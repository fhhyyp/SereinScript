# DateTimeValue 类型分析报告

## 1. 原始需求

分析 ScriptLang 中是否需要添加 `DateTimeValue` 类型，以及添加后对**词法解析（Lexer）**、**语法解析（Parser）**、**编译（Compiler）** 和 **VM 执行（VM）** 的影响。

---

## 2. 现状分析

### 2.1 当前 Value 类型体系

```
Value (abstract base class)
├── NullValue              - 单例
├── NumberValue<TNumber>   - 泛型数值：int/long/float/double/decimal
├── MutableNumber          - 可变数值容器（var 声明，原地运算，零分配）
├── StringValue            - 字符串
├── BoolValue              - 布尔（True/False 单例）
├── ObjectValue            - 字典 map/record
├── ArrayValue             - 列表
├── ClrObjectValue         - 包装任意 C# 对象
├── ClrMethodValue         - 包装 MethodInfo
├── FunctionValue          - 原生函数（实现 ICallable）
├── CompiledFunctionValue  - 编译后的 DSL Lambda（实现 ICallable）
├── RangeIterator          - 惰性范围迭代器
```

### 2.2 当前 DateTime 相关支持

| 位置 | 实现 | 说明 |
|------|------|------|
| [BuiltinCache.cs:14-15](d:\Project\C#\SereinScript\SereinScript\ScriptLang\BuiltinCache.cs#L14-L15) | `now` 内置函数 | 返回 `DateTime.Now.Ticks` 作为 `NumberValue<long>` |
| [BuiltinCache.cs:105-108](d:\Project\C#\SereinScript\SereinScript\ScriptLang\BuiltinCache.cs#L105-L108) | 注释的 `nowtime` | 被注释掉，原计划返回 `DateTime.Now.ToString()` 作为 StringValue |

**结论：当前没有专用的 DateTimeValue 类型。** 仅有的 `now` 函数返回的是 Tick 计数值（`long` 类型），脚本层无法直接判断这是一个日期时间值，也无法进行日期运算（如加一天、格式化输出等）。

### 2.3 Value 管道的完整数据流

```
源文件文本
    │
    ▼
[Lexer]  → Token（Literal 字段承载原始 C# 值：int/long/float/double/decimal/string/bool/null）
    │
    ▼
[Parser] → AST（LiteralExpr 承载 object? Value，即 Token.Literal）
    │
    ▼
[Compiler] → ByteCode（通过 _chunk.AddConstant(value) 存入常量表，发射 LoadConst）
    │
    ▼
[VM.ValueFromConstant] → Value 运行时对象（通过 switch 匹配 C# 类型创建对应 Value 子类）
    │
    ▼
[VM 运算] → 通过 IsXxx 属性做类型分派（AddOp/SubOp/.../Compare/IsEqual/IsTrue）
```

---

## 3. 是否有必要添加 DateTimeValue？

### 3.1 需要 DateTimeValue 的场景（需求驱动）

| 场景 | 当前可行？ | 说明 |
|------|-----------|------|
| 获取当前时间 | ✅ 可行 | `now` 返回 Ticks（long），脚本可拿到数值 |
| 日期比较（早晚） | ✅ 可行 | Ticks 是 long，可直接用 `<` `>` 比较 |
| 日期格式化输出 | ❌ 不可行 | Ticks 无法直接转为 "2026-06-11" 格式 |
| 日期加减（如加 3 天） | ❌ 困难 | 需要用 Ticks + 3 * 86400 * 10^7，脚本层不直观 |
| 提取年/月/日 | ❌ 不可行 | 需要 `new DateTime(ticks).Year` 等 CLR 互操作 |
| 解析日期字符串 | ❌ 不可行 | "2026-06-11" 只是普通 string，无法转为日期 |
| 日期字面量语法 | ❌ 不可行 | 无 `#2026-06-11#` 或 `d"2026-06-11"` 等语法 |
| JSON 序列化/反序列化 | ❌ 不可行 | 与外部系统交互时日期格式是关键需求 |

### 3.2 推荐方案评估

| 方案 | 复杂度 | 描述 |
|------|--------|------|
| **方案 A：新增 DateTimeValue** | 高 | 完整实现新值类型，含词法/语法/编译/VM 全链路 |
| **方案 B：增强 ClrObjectValue 路径** | 中 | 利用已有 ClrObjectValue 包装 DateTime，增加内置函数支持 |
| **方案 C：保持现状 + 内置函数增强** | 低 | 添加 `date_format`、`date_parse` 等辅助函数，值仍用 long Ticks 表示 |

### 3.3 推荐

**建议采取渐进策略：先实施方案 C，为方案 A 做架构预留。**

理由：
1. 当前 `ClrObjectValue` 已能包装任意 CLR 对象（包括 `DateTime`），通过 `ConvertClrToValue` 的 `_ => new ClrObjectValue(clrValue)` 分支即可自动处理
2. 如果脚本只需要进行日期计算和格式化，增加内置函数（`date_add_days`、`date_format` 等）足够覆盖大部分场景
3. 如果未来确认需要日期字面量语法和原生 DateTime 类型，方案 A 的设计可以基于方案 C 平滑升级

**但是，如果以下条件任一成立，应直接实施方案 A：**
- 脚本需要频繁处理日期（如配置调度脚本）
- 需要日期字面量语法提升可读性
- 需要与其他系统通过 JSON 交换日期数据
- 类型系统需要 `typeof()` 能区分日期值

---

## 4. 方案 A：新增 DateTimeValue 的完整影响分析

### 4.1 DateTimeValue 类型设计

```csharp
/// <summary>
/// 日期时间值（不可变）
/// </summary>
public class DateTimeValue(DateTime value) : Value
{
    public DateTime Value { get; } = value;

    public override bool IsDateTime => true;  // 新增类型判断属性

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(DateTimeValue)) return (T)(object)this;
        if (typeof(T) == typeof(DateTime)) return (T)(object)Value;
        if (typeof(T) == typeof(long)) return (T)(object)Value.Ticks;
        if (typeof(T) == typeof(string)) return (T)(object)Value.ToString("O");
        throw new InvalidCastException($"无法将 DateTimeValue 转换为 {typeof(T)}");
    }

    public override string ToString() => Value.ToString("O"); // ISO 8601
}
```

### 4.2 各层影响详细分析

#### 4.2.1 Value 基类（[Value.cs](d:\Project\C#\SereinScript\SereinScript\ScriptLang\Runtime\Value.cs)）

**变更点：**

| 变更项 | 位置 | 描述 |
|--------|------|------|
| `IsDateTime` 属性 | 基类 `Value` | 新增 `public virtual bool IsDateTime => false;` |
| `IsDateTime` 重写 | `DateTimeValue` | 新增 `public override bool IsDateTime => true;` |
| `ToString()` switch | 基类 `Value` | 新增 `DateTimeValue dt => dt.Value.ToString("O")` 分支 |

**影响程度：⭐ 极小** — 仅新增约 5 行代码，不影响已有类型。

---

#### 4.2.2 词法分析器（[Lexer.cs](d:\Project\C#\SereinScript\SereinScript\ScriptLang\Lexer\Lexer.cs)）和 TokenType

**是否需要新增 TokenType？**

这取决于是否支持日期字面量语法。有两种可选语法：

| 语法风格 | 示例 | Lexer 改动 |
|----------|------|------------|
| 无字面量语法（推荐初期） | 仅通过 `date("2026-06-11")` 内置函数创建 | **无 Lexer 改动** |
| 前缀语法 | `d"2026-06-11"` 或 `dt"2026-06-11"` | 需在 `ScanToken` 的 `default` 分支识别 `d"` 前缀 |
| `#` 语法 | `#2026-06-11#` | 需在 `ScanToken` 添加 `#` 分支 |

**如果选择「无字面量语法」：**
- **TokenType 枚举（[TokenType.cs](d:\Project\C#\SereinScript\SereinScript\ScriptLang\Lexer\TokenType.cs)）：无需改动**
- **Lexer：无需改动**
- 日期值完全通过内置函数在运行时创建

**如果选择「前缀语法 d"..."」：**

需在 `ScanToken()` 方法（约第 161 行 `default` 分支）中，在 `char.IsLetter(c)` 判断之前，增加对 `d"` 和 `dt"` 的识别：

```csharp
// 在 ReadIdentifier() 之前检测日期字面量
if ((c == 'd' || c == 'D') && Peek() == '"')
{
    Advance(); // 跳过 d
    ReadDateTimeString(); // 读取 "2026-06-11" 部分
}
```

并在 `TokenType` 枚举中新增：
```csharp
DateTime,  // d"2026-06-11"
```

`Token.Literal` 中存储解析后的 `DateTime` 对象。

**影响程度：⭐⭐ 中等（如果有字面量语法）/ ⭐ 无（如果无字面量语法）**

---

#### 4.2.3 语法解析器（[Parser.cs](d:\Project\C#\SereinScript\SereinScript\ScriptLang\Parser\Parser.cs)）

**变更点：**

如果有日期字面量 TokenType，需在 `ParsePrimary()` 方法（第 443 行）的字面量匹配中增加：

```csharp
// 从（第447行）：
if (Match(TokenType.Number_Int, ..., TokenType.Null))

// 改为：
if (Match(TokenType.Number_Int, ..., TokenType.Null, TokenType.DateTime))
```

**AST：[LiteralExpr](d:\Project\C#\SereinScript\SereinScript\ScriptLang\Parser\Ast.cs#L54)** 无需改动 — 它已接受 `object? Value`，`DateTime` 可以直接存放。

**影响程度：⭐ 极小（如果有字面量语法）/ ⭐ 无（如果无字面量语法）**

---

#### 4.2.4 编译器（[Compiler.cs](d:\Project\C#\SereinScript\SereinScript\ScriptLang\Runtime\ByteCode\Compiler.cs)）

**变更点：**

[`CompileLiteral` 方法（第326行）](d:\Project\C#\SereinScript\SereinScript\ScriptLang\Runtime\ByteCode\Compiler.cs#L326-L352)：

`DateTime` 不属于现有的快速路径（null/true/false/-1/0/1），会走默认分支：

```csharp
int constIndex = _chunk.AddConstant(value);  // DateTime 作为常量存入
Emit(OpCode.LoadConst, constIndex);
```

**ByteCodeChunk 常量表** 需要能存储 `DateTime` 对象。需要确认 `ByteCodeChunk.AddConstant()` 是否接受任意 `object?`。

**编译器本身不需要改动** — 只要常量表能存 `DateTime`，`LoadConst` 就能正常加载。

**影响程度：⭐ 极小（前提：常量表支持 DateTime）**

---

#### 4.2.5 VM 执行引擎（[VM.cs](d:\Project\C#\SereinScript\SereinScript\ScriptLang\Runtime\ByteCode\VM.cs)）

这是影响最大的层级。需要在多个方法中添加 `DateTimeValue` 的处理：

##### (a) `ValueFromConstant`（第1562-1576行）

```csharp
private static Value ValueFromConstant(object? constant)
{
    return constant switch
    {
        null => Value.Null,
        int i => ..., long l => ..., /* ... */
        bool b => BoolValue.Create(b),
        DateTime dt => new DateTimeValue(dt),  // ← 新增
        _ => throw new RuntimeException($"不支持的常量类型: {constant?.GetType()}")
    };
}
```

##### (b) `ConvertClrToValue`（第1592-1606行）

```csharp
private static Value ConvertClrToValue(object? clrValue)
{
    return clrValue switch
    {
        null => Value.Null,
        int i => ..., long l => ..., /* ... */
        bool b => BoolValue.Create(b),
        DateTime dt => new DateTimeValue(dt),  // ← 新增
        _ => new ClrObjectValue(clrValue)
    };
}
```

##### (c) `ConvertValueToClr`（第1578-1590行）

```csharp
private static object? ConvertValueToClr(Value value, Type targetType)
{
    // ...
    if (value is DateTimeValue dt) return dt.Value;  // ← 新增
    if (value is ClrObjectValue clr) return clr.Value;
    // ...
}
```

##### (d) `ToString()` switch 在 Value 基类

新增 `DateTimeValue dt => dt.Value.ToString("O")` 分支。

##### (e) `IsTrue`（第1528-1538行）

DateTimeValue 默认返回 `false` — 无需改动（走 `_ => false` 分支）。

##### (f) `IsEqual`（第1541-1559行）

两个 `DateTimeValue` 比较应使用 `dt1.Value == dt2.Value`。当前走 `_ => left.Equals(right)` 分支，若 `DateTimeValue` 重写 `Equals` 则可正常工作，但建议显式添加：

```csharp
(DateTimeValue d1, DateTimeValue d2) => d1.Value == d2.Value,
```

##### (g) 比较运算 `Compare`（第1482-1511行）

当前仅支持数值和字符串比较。如果期望 `date1 < date2` 这样的比较，需要新增分支：

```csharp
if (left.IsDateTime && right.IsDateTime)
{
    int cmp = left.As<DateTime>().CompareTo(right.As<DateTime>());
    return BoolValue.Create(kind switch { ... });
}
```

或者不直接支持日期比较运算符，由内置函数 `date_compare` 处理。

##### (h) 算术运算（AddOp/SubOp）

**不建议**让 `date + 3` 直接工作（语义不明确：加 3 天？小时？秒？），应在内置函数中提供 `date_add(date, days, "days")` 等明确函数。

##### (i) MutableNumber 的 SetFrom/FromNumberValue

**DateTimeValue 不应支持 MutableNumber 转换。** 日期是不可变值，`SetFrom` 应抛出异常或忽略 DateTimeValue。

##### (j) PrototypeManager / GetMember

如果 `DateTimeValue` 需要属性访问（如 `.Year`, `.Month`, `.Day`），可以通过 PrototypeManager 注册原型方法，或直接让 `GetMember` 在遇到 `DateTimeValue` 时反射访问 `DateTime` 的属性。

**影响程度：⭐⭐⭐ 中等** — 核心路径 `ValueFromConstant`、`ConvertClrToValue`、`ConvertValueToClr` 必须修改；比较/相等性逻辑需要决策。

---

#### 4.2.6 内置函数（[BuiltinCache.cs](d:\Project\C#\SereinScript\SereinScript\ScriptLang\BuiltinCache.cs)）

**推荐新增的内置函数：**

| 函数 | 签名 | 说明 |
|------|------|------|
| `now` | `() -> DateTimeValue` | 改造现有函数，返回 DateTimeValue 而非 Ticks |
| `date` | `(str) -> DateTimeValue` | 解析日期字符串，如 `date("2026-06-11")` |
| `date_format` | `(dt, format) -> StringValue` | 格式化输出，如 `date_format(now(), "yyyy-MM-dd")` |
| `date_add` | `(dt, amount, unit) -> DateTimeValue` | 日期加减，如 `date_add(now(), 3, "days")` |
| `date_diff` | `(dt1, dt2, unit) -> NumberValue` | 日期差值 |

**`typeof` 函数**也需要新增 `DateTimeValue => "datetime"` 分支。

**影响程度：⭐⭐ 中等** — 需要设计并实现 4-6 个新内置函数。

---

### 4.3 影响汇总表

| 层级 | 文件 | 无字面量方案 | 有字面量方案 |
|------|------|-------------|-------------|
| Value 类型 | `Value.cs` | ⭐ 新增 ~30 行 | ⭐ 新增 ~30 行 |
| Token 类型 | `TokenType.cs` | 无改动 | ⭐ 新增 1 行 |
| Lexer | `Lexer.cs` | 无改动 | ⭐⭐ 新增 ~40 行 |
| Parser | `Parser.cs` | 无改动 | ⭐ 新增 1 行 |
| AST | `Ast.cs` | 无改动 | 无改动 |
| Compiler | `Compiler.cs` | 无改动 | 无改动 |
| VM 核心 | `VM.cs` | ⭐⭐ ~20 行改动 | ⭐⭐ ~20 行改动 |
| 内置函数 | `BuiltinCache.cs` | ⭐⭐ 新增 ~80 行 | ⭐⭐ 新增 ~80 行 |

---

## 5. 风险评估

| 风险 | 等级 | 缓解措施 |
|------|------|----------|
| 破坏现有 `now` 函数返回值类型 | 🔴 高 | 保留 `now_ticks()` 返回 long，新增 `now()` 返回 DateTimeValue |
| 常量表序列化兼容性 | 🟡 中 | `DateTime` 可被 `System.Text.Json` 直接序列化 |
| 闭包捕获 DateTimeValue 的正确性 | 🟢 低 | DateTimeValue 是不可变值，与 NumberValue 相同语义 |
| MutableNumber 误转换 | 🟢 低 | `FromNumberValue` 和 `SetFrom` 仅处理数值类型，不会误判 |
| 多时区/时区语义 | 🟡 中 | `DateTime` 的 Kind（UTC/Local/Unspecified）需要在设计中明确处理策略 |

---

## 6. 结论与建议

### 推荐实施路线

**阶段 1：无字面量方案（低风险，快速交付）**
1. 新增 `DateTimeValue` 类（约 30 行）
2. 修改 `VM.ValueFromConstant` 和 `VM.ConvertClrToValue`（约 5 行）
3. 改造 `now` 函数返回 `DateTimeValue`（保留 `now_ticks` 兼容）
4. 新增 `date`、`date_format` 内置函数
5. 更新 `typeof` 函数

**阶段 2（可选）：添加字面量语法**
1. 新增 `TokenType.DateTime`
2. Lexer 支持 `d"..."` 前缀语法
3. Parser 的 `ParsePrimary` 增加匹配

### 关键设计决策

1. **值语义**：`DateTimeValue` 应为不可变值（与 `NumberValue`、`StringValue` 一致）
2. **时区处理**：建议统一使用 UTC 存储，仅在格式化时转换
3. **与 ClrObjectValue 的关系**：`DateTimeValue` 可作为 `ClrObjectValue` 的特化版，提供更精确的类型判断和更好的性能

---

## 7. 待确认问题

1. **是否需要日期字面量语法？** 如果不需要，可以完全跳过 Lexer/Parser 的改动
2. **时区策略？** 内部存储 UTC 还是 Local？建议 UTC
3. **是否需要 TimeSpan 类型？** 日期差值返回什么类型？建议先用 `NumberValue<double>`（总天数）或 `NumberValue<long>`（Ticks 差值）
4. **与 JSON 模块的交互？** 序列化时 DateTimeValue 应输出为 ISO 8601 字符串
