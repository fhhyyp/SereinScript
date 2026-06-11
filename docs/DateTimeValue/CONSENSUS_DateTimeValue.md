# DateTimeValue 共识文档

## 1. 需求确认

基于 [ALIGNMENT_DateTimeValue.md](./ALIGNMENT_DateTimeValue.md) 分析报告，结合已确认的 5 项需求，形成以下共识。

| # | 需求 | 确认状态 | 对应报告结论 |
|---|------|---------|-------------|
| 1 | 不需要字面量语法，跳过 Lexer/Parser 改动，新增 `date` 内置函数 | ✅ 确认 | 报告「阶段 1：无字面量方案」完全匹配 |
| 2 | 使用 UTC 存储 | ✅ 确认 | 报告 §6 关键设计决策 #2 |
| 3 | 支持加法和减法，计算天数差异/时间差异，通过**操作符**实现 | ✅ 确认 | 超越报告建议：报告 §4.2.5(h) 不建议运算符重载，需求明确通过 `+` `-` 操作符实现 |
| 4 | Prototype 支持获取年/月/日/时/分/秒/毫秒，以及 `.ToString()` 序列化 | ✅ 确认 | 报告 §4.2.5(j) 简要提及，需深化设计 |
| 5 | JSON 模块：序列化时 DateTimeValue → ISO 8601 字符串 | ✅ 确认 | 报告 §7 待确认问题 #4 |

---

## 2. 需求理解与技术设计

### 2.1 DateTimeValue 类型设计

```
DateTimeValue : Value  (不可变值类型)
├── 内部存储: DateTime (Kind = Utc)
├── IsDateTime => true
├── As<T>() → DateTime / long(Ticks) / string(ISO 8601)
├── ToString() → ISO 8601 格式 "2026-06-11T10:30:00.0000000Z"
├── Equals() → 基于 Value (DateTime) 比较
└── GetHashCode() → Value.GetHashCode()
```

**关键设计决策：**

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 内部存储类型 | `System.DateTime` (UTC) | 足够精确到 100ns，相比 DateTimeOffset 更轻量 |
| 值的可变性 | 不可变 (Immutable) | 与 NumberValue、StringValue 一致，避免共享状态问题 |
| 是否继承 ClrObjectValue | **否**，直接继承 Value | 需独立 `IsDateTime` 类型判断，且 Prototype 需明确 target 类型 |

### 2.2 TimeSpanValue 类型设计

为支持 `DateTime - DateTime → TimeSpan` 的 C# 语义，需新增差值类型：

```
TimeSpanValue : Value  (不可变值类型)
├── 内部存储: TimeSpan
├── IsTimeSpan => true
├── As<T>() → TimeSpan / long(Ticks) / double(totalDays)
├── ToString() → "3.00:00:00" (TimeSpan 标准格式)
├── Equals() → 基于 Value 比较
└── GetHashCode() → Value.GetHashCode()
```

### 2.3 算术运算设计（需求 3）— 操作符实现

**核心原则**：遵循 C# 语义，通过 VM 层 `AddOp`/`SubOp` 实现，无需 `date_add`/`date_sub`/`date_diff` 内置函数。

#### 操作符矩阵

```
脚本层语法              VM 方法      类型匹配                          返回类型
────────              ───────      ────────                          ────────
dt + ts               AddOp        DateTimeValue + TimeSpanValue      DateTimeValue
ts + dt               AddOp        TimeSpanValue + DateTimeValue      DateTimeValue
ts1 + ts2             AddOp        TimeSpanValue + TimeSpanValue      TimeSpanValue

dt - ts               SubOp        DateTimeValue - TimeSpanValue      DateTimeValue
dt1 - dt2             SubOp        DateTimeValue - DateTimeValue      TimeSpanValue
ts1 - ts2             SubOp        TimeSpanValue - TimeSpanValue      TimeSpanValue
```

#### 脚本层使用示例

```js
var dt = date("2026-06-11");
var ts = timespan(3, "days");

var later  = dt + ts;           // 3天后 → DateTimeValue
var sooner = dt - ts;           // 3天前 → DateTimeValue

var dt2 = date("2026-06-14");
var diff = dt2 - dt;           // 日期差 → TimeSpanValue
diff.totalDays;                 // 3.0
diff.days;                      // 3
diff.totalHours;                // 72.0

// 无需 date_add/date_sub/date_diff 内置函数
// typeof(diff) → "timespan"
```

#### 为什么不用内置函数而用操作符

| 对比维度 | 内置函数方式 | 操作符方式 |
|---------|------------|-----------|
| 可读性 | `date_add(d, ts)` | `d + ts` ✅ |
| 语义匹配 | 自定义语义 | 与 C#/主流语言一致 ✅ |
| 可组合性 | 返回值需再次包装 | `(dt1 - dt2).totalDays` 自然链式 ✅ |
| typeof 区分 | 返回 number，无法区分 | 返回 TimeSpanValue，类型明确 ✅ |
| VM 影响 | 无 | AddOp/SubOp 各加 ~20 行 |

### 2.4 Prototype 设计（需求 4）

遵循现有 Prototype 模式（见 [StringPrototype.cs](../../ScriptLang/Prototype/StringPrototype.cs)），创建 **两个** Prototype 类：

#### DateTimePrototype

```csharp
[PrototypeExtension(NamingFormat = NamingFormat.Js)]
public partial class DateTimePrototype
{
    public partial bool IsTarget(Value value) => value.IsDateTime;

    // ===== 属性（只读） =====
    [PrototypeProperty] static NumberValue<int> Year(DateTimeValue dt);
    [PrototypeProperty] static NumberValue<int> Month(DateTimeValue dt);
    [PrototypeProperty] static NumberValue<int> Day(DateTimeValue dt);
    [PrototypeProperty] static NumberValue<int> Hour(DateTimeValue dt);
    [PrototypeProperty] static NumberValue<int> Minute(DateTimeValue dt);
    [PrototypeProperty] static NumberValue<int> Second(DateTimeValue dt);
    [PrototypeProperty] static NumberValue<int> Millisecond(DateTimeValue dt);
    [PrototypeProperty] static NumberValue<long> Ticks(DateTimeValue dt);
    [PrototypeProperty] static NumberValue<int> DayOfWeek(DateTimeValue dt);
    [PrototypeProperty] static NumberValue<int> DayOfYear(DateTimeValue dt);

    // ===== 方法 =====
    [PrototypeFunction] static StringValue ToString(DateTimeValue dt, StringValue? format = null);
}
```

#### TimeSpanPrototype

```csharp
[PrototypeExtension(NamingFormat = NamingFormat.Js)]
public partial class TimeSpanPrototype
{
    public partial bool IsTarget(Value value) => value.IsTimeSpan;

    // ===== 整数部分属性 =====
    [PrototypeProperty] static NumberValue<int> Days(TimeSpanValue ts);
    [PrototypeProperty] static NumberValue<int> Hours(TimeSpanValue ts);
    [PrototypeProperty] static NumberValue<int> Minutes(TimeSpanValue ts);
    [PrototypeProperty] static NumberValue<int> Seconds(TimeSpanValue ts);
    [PrototypeProperty] static NumberValue<int> Milliseconds(TimeSpanValue ts);

    // ===== 总量属性（浮点） =====
    [PrototypeProperty] static NumberValue<double> TotalDays(TimeSpanValue ts);
    [PrototypeProperty] static NumberValue<double> TotalHours(TimeSpanValue ts);
    [PrototypeProperty] static NumberValue<double> TotalMinutes(TimeSpanValue ts);
    [PrototypeProperty] static NumberValue<double> TotalSeconds(TimeSpanValue ts);
    [PrototypeProperty] static NumberValue<double> TotalMilliseconds(TimeSpanValue ts);

    // ===== 方法 =====
    [PrototypeFunction] static StringValue ToString(TimeSpanValue ts, StringValue? format = null);
}
```

**Prototype 注册**：在 `ScriptEngine` 初始化时注册。

**GetMember 流程**（无需修改 VM，现有机制已支持）：
```
dt.year
  → VM.GetMember()
  → target is not ObjectValue, skip dict lookup
  → _engine.PrototypeManager.TryGetValue(target, "year", out result)
  → DateTimePrototype.IsTarget(target) == true
  → DateTimePrototype.GetMethod(target, "year") 返回 Year 属性值
  → Push(result)
```

### 2.5 JSON 模块同步（需求 5）

修改 `JsonModule.cs` 的两个核心转换方法：

#### ConvertToClrObject（序列化方向）

```csharp
DateTimeValue dt => dt.Value.ToString("O"),       // ISO 8601
TimeSpanValue ts => ts.Value.ToString(),           // TimeSpan 标准格式 "3.00:00:00"
```

#### ConvertToScriptValue（反序列化方向）

JsonElement 中的日期字符串**不会**自动转换为 DateTimeValue。需通过 `date(str)` / `timespan(num, unit)` 内置函数显式转换。

> **设计理由**：JSON 解析时无法区分 `"2026-06-11"` 是日期字符串还是普通文本，自动推断会导致歧义。

### 2.6 内置函数设计

| 函数 | 签名 | 说明 |
|------|------|------|
| `now` | `() → DateTimeValue` | **改造现有函数**：从返回 `NumberValue<long>`（Ticks）改为返回 `DateTimeValue`（UTC） |
| `date` | `(StringValue) → DateTimeValue` | 解析 ISO 8601 字符串为 DateTimeValue（UTC）。如 `date("2026-06-11")` |
| `timespan` | `(NumberValue, StringValue unit) → TimeSpanValue` | 从数量+单位构造时间跨度。如 `timespan(3, "days")` |

> **注意**：不存在 `date_add`/`date_sub`/`date_diff` 内置函数 — 这些功能由 `+` `-` 操作符覆盖。

#### `timespan` 函数支持的 unit

`"days"`, `"hours"`, `"minutes"`, `"seconds"`, `"milliseconds"`, `"ticks"`

#### `typeof` 函数更新

```csharp
DateTimeValue => "datetime",
TimeSpanValue => "timespan",
```

---

## 3. 影响范围与文件清单

### 3.1 新增文件

| 文件 | 描述 | 预估行数 |
|------|------|---------|
| `ScriptLang/Runtime/DateTimeValue.cs` | DateTimeValue 类 | ~50 行 |
| `ScriptLang/Runtime/TimeSpanValue.cs` | TimeSpanValue 类 | ~50 行 |
| `ScriptLang/Prototype/DateTimePrototype.cs` | DateTime Prototype | ~70 行 |
| `ScriptLang/Prototype/TimeSpanPrototype.cs` | TimeSpan Prototype | ~80 行 |

### 3.2 修改文件

| 文件 | 修改内容 | 影响程度 |
|------|---------|---------|
| [Value.cs](../../ScriptLang/Runtime/Value.cs#L20-L44) | 新增 `IsDateTime`、`IsTimeSpan` 属性（~4 行） | ⭐ 极小 |
| [Value.cs](../../ScriptLang/Runtime/Value.cs#L54-L74) | `ToString()` switch 新增 `DateTimeValue`、`TimeSpanValue` 分支（~3 行） | ⭐ 极小 |
| [VM.cs](../../ScriptLang/Runtime/ByteCode/VM.cs#L1329-L1355) | `AddOp` 新增 DateTime+TimeSpan、TimeSpan+DateTime、TimeSpan+TimeSpan（~20 行） | ⭐⭐ 中 |
| [VM.cs](../../ScriptLang/Runtime/ByteCode/VM.cs#L1357-L1385) | `SubOp` 新增 DateTime-TimeSpan、DateTime-DateTime、TimeSpan-TimeSpan（~20 行） | ⭐⭐ 中 |
| [VM.cs](../../ScriptLang/Runtime/ByteCode/VM.cs#L1562-L1576) | `ValueFromConstant` 新增 `DateTime`、`TimeSpan` 分支（~2 行） | ⭐ 极小 |
| [VM.cs](../../ScriptLang/Runtime/ByteCode/VM.cs#L1592-L1606) | `ConvertClrToValue` 新增 `DateTime`、`TimeSpan` 分支（~2 行） | ⭐ 极小 |
| [VM.cs](../../ScriptLang/Runtime/ByteCode/VM.cs#L1578-L1590) | `ConvertValueToClr` 新增 `DateTimeValue`、`TimeSpanValue` 分支（~2 行） | ⭐ 极小 |
| [VM.cs](../../ScriptLang/Runtime/ByteCode/VM.cs#L1541-L1560) | `IsEqual` 新增 `(DateTimeValue, DateTimeValue)`、`(TimeSpanValue, TimeSpanValue)`（~2 行） | ⭐ 极小 |
| [VM.cs](../../ScriptLang/Runtime/ByteCode/VM.cs#L1482-L1511) | `Compare` 新增 `IsDateTime`、`IsTimeSpan` 分支（~16 行） | ⭐ 小 |
| [BuiltinCache.cs](../../ScriptLang/BuiltinCache.cs#L14-L15) | `now` → DateTimeValue（UTC）；新增 `date`、`timespan`；删除 `date_add/date_sub/date_diff` 占位 | ⭐⭐ 中 |
| [BuiltinCache.cs](../../ScriptLang/BuiltinCache.cs#L126-L148) | `typeof` switch 新增 `DateTimeValue => "datetime"`、`TimeSpanValue => "timespan"` | ⭐ 极小 |
| [JsonModule.cs](../../ScriptLang/System/JsonModule.cs#L41-L55) | `ConvertToClrObject` 新增 `DateTimeValue`、`TimeSpanValue` 分支 | ⭐ 极小 |
| `ScriptLang/ScriptEngine.cs` | 注册 `DateTimePrototype`、`TimeSpanPrototype` | ⭐ 极小 |

### 3.3 不需要修改的层级

| 层级 | 原因 |
|------|------|
| Lexer (`Lexer.cs`) | 无字面量语法 |
| TokenType (`TokenType.cs`) | 同上 |
| Parser (`Parser.cs`) | 同上 |
| AST (`Ast.cs`) | 同上 |
| Compiler (`Compiler.cs`) | 同上 |
| ByteCodeChunk | 同上 |

---

## 4. 详细设计

### 4.1 DateTimeValue 类

```csharp
// ScriptLang/Runtime/DateTimeValue.cs
namespace ScriptLang.Runtime;

/// <summary>
/// 日期时间值（不可变，UTC 存储）
/// </summary>
public class DateTimeValue(DateTime value) : Value
{
    /// <summary>UTC 日期时间值</summary>
    public DateTime Value { get; } = value.Kind == DateTimeKind.Utc
        ? value
        : value.ToUniversalTime();

    public override bool IsDateTime => true;

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(DateTimeValue)) return (T)(object)this;
        if (typeof(T) == typeof(DateTime)) return (T)(object)Value;
        if (typeof(T) == typeof(long)) return (T)(object)Value.Ticks;
        if (typeof(T) == typeof(string)) return (T)(object)ToString();
        throw new InvalidCastException($"无法将 DateTimeValue 转换为 {typeof(T)}");
    }

    public override string ToString() => Value.ToString("O");

    public override bool Equals(object? obj) =>
        obj is DateTimeValue other && Value.Equals(other.Value);

    public override int GetHashCode() => Value.GetHashCode();
}
```

### 4.2 TimeSpanValue 类

```csharp
// ScriptLang/Runtime/TimeSpanValue.cs
namespace ScriptLang.Runtime;

/// <summary>
/// 时间跨度值（不可变）
/// </summary>
public class TimeSpanValue(TimeSpan value) : Value
{
    public TimeSpan Value { get; } = value;

    public override bool IsTimeSpan => true;

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(TimeSpanValue)) return (T)(object)this;
        if (typeof(T) == typeof(TimeSpan)) return (T)(object)Value;
        if (typeof(T) == typeof(long)) return (T)(object)Value.Ticks;
        if (typeof(T) == typeof(double)) return (T)(object)Value.TotalDays;
        if (typeof(T) == typeof(string)) return (T)(object)ToString();
        throw new InvalidCastException($"无法将 TimeSpanValue 转换为 {typeof(T)}");
    }

    public override string ToString() => Value.ToString();

    public override bool Equals(object? obj) =>
        obj is TimeSpanValue other && Value.Equals(other.Value);

    public override int GetHashCode() => Value.GetHashCode();
}
```

### 4.3 VM 操作符修改

#### 4.3.1 AddOp

```csharp
private static Value AddOp(Value left, Value right)
{
    // 现有：数值加法
    if (left.IsNumber && right.IsNumber) { /* ... 不变 ... */ }

    // 现有：字符串拼接
    if (left.IsString || right.IsString)
        return StringValue.Create(left.AsString() + right.AsString());

    // 现有：数组拼接
    if (left is ArrayValue leftArr) { /* ... 不变 ... */ }

    // === 新增：DateTime / TimeSpan 操作 ===
    if (left.IsDateTime && right.IsTimeSpan)
        return new DateTimeValue(left.As<DateTime>() + right.As<TimeSpan>());

    if (left.IsTimeSpan && right.IsDateTime)
        return new DateTimeValue(right.As<DateTime>() + left.As<TimeSpan>());

    if (left.IsTimeSpan && right.IsTimeSpan)
        return new TimeSpanValue(left.As<TimeSpan>() + right.As<TimeSpan>());

    throw new RuntimeException($"不支持的操作: {left} + {right}");
}
```

#### 4.3.2 SubOp

```csharp
private static Value SubOp(Value left, Value right)
{
    // 现有：数值减法
    if (left.IsNumber && right.IsNumber) { /* ... 不变 ... */ }

    // 现有：数组差集
    if (left.IsArray) { /* ... 不变 ... */ }

    // === 新增：DateTime / TimeSpan 操作 ===
    if (left.IsDateTime && right.IsTimeSpan)
        return new DateTimeValue(left.As<DateTime>() - right.As<TimeSpan>());

    if (left.IsDateTime && right.IsDateTime)
        return new TimeSpanValue(left.As<DateTime>() - right.As<DateTime>());

    if (left.IsTimeSpan && right.IsTimeSpan)
        return new TimeSpanValue(left.As<TimeSpan>() - right.As<TimeSpan>());

    throw new RuntimeException($"不支持的操作: {left} - {right}");
}
```

#### 4.3.3 其他 VM 路径

```csharp
// ValueFromConstant — 新增：
DateTime dt => new DateTimeValue(dt),
TimeSpan ts => new TimeSpanValue(ts),

// ConvertClrToValue — 新增（必须在 _ => new ClrObjectValue 之前）：
DateTime dt => new DateTimeValue(dt),
TimeSpan ts => new TimeSpanValue(ts),

// ConvertValueToClr — 新增：
if (value is DateTimeValue dt) return dt.Value;
if (value is TimeSpanValue ts) return ts.Value;

// IsEqual — 新增 tuple 模式：
(DateTimeValue d1, DateTimeValue d2) => d1.Value == d2.Value,
(TimeSpanValue t1, TimeSpanValue t2) => t1.Value == t2.Value,

// Compare — 新增块：
if (left.IsDateTime && right.IsDateTime)
{
    int cmp = left.As<DateTime>().CompareTo(right.As<DateTime>());
    return BoolValue.Create(kind switch { /* ... */ });
}
if (left.IsTimeSpan && right.IsTimeSpan)
{
    int cmp = left.As<TimeSpan>().CompareTo(right.As<TimeSpan>());
    return BoolValue.Create(kind switch { /* ... */ });
}
```

### 4.4 内置函数实现

#### now

```csharp
private static readonly FunctionValue now = new(nameof(now),
    static () => new DateTimeValue(DateTime.UtcNow));
```

> **破坏性变更**：现有 `now` 返回 `NumberValue<long>`（Ticks），改为返回 `DateTimeValue`。

#### date

```csharp
private static readonly FunctionValue date = new(nameof(date),
    static (List<Value> args) =>
    {
        if (args.Count != 1 || args[0] is not StringValue s)
            throw new RuntimeException("date() 期望 1 个字符串参数 (ISO 8601 格式)");
        if (DateTime.TryParse(s.Value, null, DateTimeStyles.RoundtripKind, out var dt))
            return new DateTimeValue(dt);  // 构造函数自动转 UTC
        throw new RuntimeException($"无法解析日期字符串: '{s.Value}'");
    });
```

#### timespan

```csharp
private static readonly FunctionValue timespan = new(nameof(timespan),
    static (List<Value> args) =>
    {
        if (args.Count != 2 || !args[0].IsNumber || args[1] is not StringValue unit)
            throw new RuntimeException("timespan() 期望 (NumberValue, StringValue unit)");

        double amount = args[0].As<double>();
        TimeSpan ts = unit.Value switch
        {
            "days" => TimeSpan.FromDays(amount),
            "hours" => TimeSpan.FromHours(amount),
            "minutes" => TimeSpan.FromMinutes(amount),
            "seconds" => TimeSpan.FromSeconds(amount),
            "milliseconds" => TimeSpan.FromMilliseconds(amount),
            "ticks" => TimeSpan.FromTicks((long)amount),
            _ => throw new RuntimeException($"不支持的时间单位: '{unit.Value}'，支持: days/hours/minutes/seconds/milliseconds/ticks")
        };
        return new TimeSpanValue(ts);
    });
```

### 4.5 DateTimePrototype

```csharp
// ScriptLang/Prototype/DateTimePrototype.cs
namespace ScriptLang.Prototype;

[PrototypeExtension(NamingFormat = NamingFormat.Js)]
public partial class DateTimePrototype
{
    public partial bool IsTarget(Value value) => value.IsDateTime;

    [PrototypeProperty] private static NumberValue<int> Year(DateTimeValue dt)     => NumberValueFactory.Create(dt.Value.Year);
    [PrototypeProperty] private static NumberValue<int> Month(DateTimeValue dt)    => NumberValueFactory.Create(dt.Value.Month);
    [PrototypeProperty] private static NumberValue<int> Day(DateTimeValue dt)      => NumberValueFactory.Create(dt.Value.Day);
    [PrototypeProperty] private static NumberValue<int> Hour(DateTimeValue dt)     => NumberValueFactory.Create(dt.Value.Hour);
    [PrototypeProperty] private static NumberValue<int> Minute(DateTimeValue dt)   => NumberValueFactory.Create(dt.Value.Minute);
    [PrototypeProperty] private static NumberValue<int> Second(DateTimeValue dt)   => NumberValueFactory.Create(dt.Value.Second);
    [PrototypeProperty] private static NumberValue<int> Millisecond(DateTimeValue dt) => NumberValueFactory.Create(dt.Value.Millisecond);
    [PrototypeProperty] private static NumberValue<long> Ticks(DateTimeValue dt)   => NumberValueFactory.Create(dt.Value.Ticks);
    [PrototypeProperty] private static NumberValue<int> DayOfWeek(DateTimeValue dt) => NumberValueFactory.Create((int)dt.Value.DayOfWeek);
    [PrototypeProperty] private static NumberValue<int> DayOfYear(DateTimeValue dt) => NumberValueFactory.Create(dt.Value.DayOfYear);

    [PrototypeFunction]
    private static StringValue ToString(DateTimeValue dt, StringValue? format = null)
    {
        if (format != null)
            return StringValue.Create(dt.Value.ToString(format.Value));
        return StringValue.Create(dt.Value.ToString("O"));
    }
}
```

### 4.6 TimeSpanPrototype

```csharp
// ScriptLang/Prototype/TimeSpanPrototype.cs
namespace ScriptLang.Prototype;

[PrototypeExtension(NamingFormat = NamingFormat.Js)]
public partial class TimeSpanPrototype
{
    public partial bool IsTarget(Value value) => value.IsTimeSpan;

    [PrototypeProperty] private static NumberValue<int> Days(TimeSpanValue ts)          => NumberValueFactory.Create(ts.Value.Days);
    [PrototypeProperty] private static NumberValue<int> Hours(TimeSpanValue ts)         => NumberValueFactory.Create(ts.Value.Hours);
    [PrototypeProperty] private static NumberValue<int> Minutes(TimeSpanValue ts)       => NumberValueFactory.Create(ts.Value.Minutes);
    [PrototypeProperty] private static NumberValue<int> Seconds(TimeSpanValue ts)       => NumberValueFactory.Create(ts.Value.Seconds);
    [PrototypeProperty] private static NumberValue<int> Milliseconds(TimeSpanValue ts)  => NumberValueFactory.Create(ts.Value.Milliseconds);

    [PrototypeProperty] private static NumberValue<double> TotalDays(TimeSpanValue ts)          => NumberValueFactory.Create(ts.Value.TotalDays);
    [PrototypeProperty] private static NumberValue<double> TotalHours(TimeSpanValue ts)         => NumberValueFactory.Create(ts.Value.TotalHours);
    [PrototypeProperty] private static NumberValue<double> TotalMinutes(TimeSpanValue ts)       => NumberValueFactory.Create(ts.Value.TotalMinutes);
    [PrototypeProperty] private static NumberValue<double> TotalSeconds(TimeSpanValue ts)       => NumberValueFactory.Create(ts.Value.TotalSeconds);
    [PrototypeProperty] private static NumberValue<double> TotalMilliseconds(TimeSpanValue ts)  => NumberValueFactory.Create(ts.Value.TotalMilliseconds);
    [PrototypeProperty] private static NumberValue<long> Ticks(TimeSpanValue ts)                => NumberValueFactory.Create(ts.Value.Ticks);

    [PrototypeFunction]
    private static StringValue ToString(TimeSpanValue ts, StringValue? format = null)
    {
        if (format != null)
            return StringValue.Create(ts.Value.ToString(format.Value));
        return StringValue.Create(ts.Value.ToString());
    }
}
```

### 4.7 JSON 模块修改

```csharp
// JsonModule.cs - ConvertToClrObject
private static object? ConvertToClrObject(Value value)
{
    return value switch
    {
        // ... 现有分支 ...
        DateTimeValue dt => dt.Value.ToString("O"),     // ← 新增
        TimeSpanValue ts => ts.Value.ToString(),         // ← 新增
        _ => value.ToString()
    };
}
```

---

## 5. 数据流图

```
脚本层                            VM层                              C#/CLR层
───────                          ──────                            ───────

date("2026-06-11")  ───→  BuiltinCache.date()  ───→  DateTime.TryParse()
                                    │                          │
                                    ▼                          ▼
now()  ───→  BuiltinCache.now()  ───→  DateTime.UtcNow
                                    │                          │
                                    ▼                          ▼
                            new DateTimeValue(dt)    ───  DateTime(Kind=Utc)


timespan(3,"days")  ───→  BuiltinCache.timespan()  ───→  TimeSpan.FromDays(3)
                                    │                          │
                                    ▼                          ▼
                            new TimeSpanValue(ts)    ───  TimeSpan


dt + ts  ───→  VM.AddOp()  ───→  DateTimeValue + TimeSpanValue
                                    │                          │
                                    ▼                          ▼
                            new DateTimeValue(dt+ts)    ───  DateTime


dt1 - dt2  ───→  VM.SubOp()  ───→  DateTimeValue - DateTimeValue
                                    │                          │
                                    ▼                          ▼
                            new TimeSpanValue(dt1-dt2)  ───  TimeSpan


dt.year  ───→  VM.GetMember()  ───→  PrototypeManager.TryGetValue()
                                    │                    │
                                    ▼                    ▼
                            DateTimePrototype.IsTarget() == true
                            DateTimePrototype.Year(dt)
                                    │
                                    ▼
                            NumberValue<int>(2026)
```

---

## 6. 风险评估

| 风险 | 等级 | 缓解措施 |
|------|------|----------|
| 现有 `now` 函数破坏性变更 | 🔴 高 | 发布说明明确标注；评估是否需要 `now_ticks` 兼容函数 |
| `ConvertClrToValue` 分支顺序 | 🟡 中 | `DateTime`/`TimeSpan` 分支**必须**在 `_ => new ClrObjectValue` 之前 |
| 时区陷阱：非 UTC 的 DateTime 传入 | 🟡 中 | DateTimeValue 构造函数自动 `.ToUniversalTime()` |
| `DateTime - DateTime` 跨界溢出 | 🟢 低 | TimeSpan 内部使用 long Ticks，范围 ~29,000 年，足够 |
| Prototype 源生成器兼容性 | 🟢 低 | 完全遵循现有 StringPrototype 模式 |
| JSON 反序列化：日期字符串自动推断 | 🟢 低 | 明确策略：不自动推断，由脚本层 `date()` 显式转换 |
| AddOp/SubOp 类型组合爆炸 | 🟡 中 | 仅添加 3+3=6 种组合（DateTime/TimeSpan 相互），不扩展 NumberValue 混用 |

---

## 7. 验收标准

### 7.1 DateTimeValue 类型

- [ ] `DateTimeValue` 类存在且继承 `Value`
- [ ] `IsDateTime` 属性返回 `true`
- [ ] `As<DateTime>()` 返回内部 DateTime（UTC）
- [ ] `As<long>()` 返回 Ticks
- [ ] `As<string>()` 返回 ISO 8601 格式字符串
- [ ] `ToString()` 输出 ISO 8601 格式 `"2026-06-11T10:30:00.0000000Z"`
- [ ] 构造函数自动将非 UTC 时间转为 UTC
- [ ] `Equals()` 基于 DateTime 值比较

### 7.2 TimeSpanValue 类型

- [ ] `TimeSpanValue` 类存在且继承 `Value`
- [ ] `IsTimeSpan` 属性返回 `true`
- [ ] `As<TimeSpan>()` 返回内部 TimeSpan
- [ ] `As<double>()` 返回 TotalDays
- [ ] `As<long>()` 返回 Ticks
- [ ] `ToString()` 输出 TimeSpan 标准格式

### 7.3 操作符

- [ ] `DateTimeValue + TimeSpanValue → DateTimeValue`
- [ ] `TimeSpanValue + DateTimeValue → DateTimeValue`
- [ ] `TimeSpanValue + TimeSpanValue → TimeSpanValue`
- [ ] `DateTimeValue - TimeSpanValue → DateTimeValue`
- [ ] `DateTimeValue - DateTimeValue → TimeSpanValue`
- [ ] `TimeSpanValue - TimeSpanValue → TimeSpanValue`
- [ ] `DateTimeValue == DateTimeValue` 正确比较
- [ ] `DateTimeValue < DateTimeValue` 等比较运算符正确
- [ ] 不支持的操作组合（如 `DateTimeValue + NumberValue`）抛出 RuntimeException

### 7.4 内置函数

- [ ] `now()` 返回当前 UTC 时间的 DateTimeValue
- [ ] `date("2026-06-11")` 返回对应 UTC DateTimeValue
- [ ] `date("2026-06-11T10:30:00Z")` 解析带时区标记的 ISO 8601
- [ ] `date("invalid")` 抛出 RuntimeException
- [ ] `timespan(3, "days")` 返回 3 天的 TimeSpanValue
- [ ] `timespan(num, "invalid")` 抛出 RuntimeException
- [ ] `typeof(date("..."))` 返回 `"datetime"`
- [ ] `typeof(timespan(1, "days"))` 返回 `"timespan"`

### 7.5 Prototype 属性

#### DateTimePrototype

- [ ] `.year` / `.month` / `.day` 返回正确的 `NumberValue<int>`
- [ ] `.hour` / `.minute` / `.second` / `.millisecond` 返回正确的 `NumberValue<int>`
- [ ] `.dayOfWeek` / `.dayOfYear` 返回正确的 `NumberValue<int>`
- [ ] `.ticks` 返回 `NumberValue<long>`
- [ ] `.toString()` 返回 ISO 8601 字符串
- [ ] `.toString("yyyy-MM-dd")` 返回自定义格式字符串

#### TimeSpanPrototype

- [ ] `.days` / `.hours` / `.minutes` / `.seconds` / `.milliseconds` 返回正确的整数分量
- [ ] `.totalDays` / `.totalHours` / `.totalMinutes` / `.totalSeconds` / `.totalMilliseconds` 返回正确浮点值
- [ ] `.ticks` 返回 `NumberValue<long>`
- [ ] `.toString()` 返回 TimeSpan 标准格式

### 7.6 VM 类型系统

- [ ] `ValueFromConstant(DateTime)` → DateTimeValue
- [ ] `ValueFromConstant(TimeSpan)` → TimeSpanValue
- [ ] `ConvertClrToValue(DateTime)` → DateTimeValue（**非** ClrObjectValue）
- [ ] `ConvertClrToValue(TimeSpan)` → TimeSpanValue（**非** ClrObjectValue）
- [ ] `ConvertValueToClr(DateTimeValue)` → `DateTime`
- [ ] `ConvertValueToClr(TimeSpanValue)` → `TimeSpan`
- [ ] `IsEqual(dt1, dt2)` 正确比较
- [ ] `IsEqual(ts1, ts2)` 正确比较
- [ ] `ConvertClrToValue` 中 DateTime/TimeSpan 分支在 `_ => ClrObjectValue` 之前

### 7.7 JSON 模块

- [ ] `json.stringify({ t: now() })` 输出 `{"t":"<ISO 8601>"}`
- [ ] `json.stringify({ span: dt1 - dt2 })` 输出 `{"span":"<TimeSpan 格式>"}`
- [ ] DateTimeValue 序列化为字符串，非 Ticks 数字或对象

### 7.8 回归测试

- [ ] 现有 `now` 的使用场景不受影响（或明确标记为破坏性变更）
- [ ] 所有现有测试通过
- [ ] `ClrObjectValue` 包装非 DateTime/TimeSpan 对象的行为不变
- [ ] 现有 `+` `-` 操作符（数值、字符串、数组）行为不变

---

## 8. 与原始报告的差异总览

| 方面 | ALIGNMENT 报告 | CONSENSUS v1 | **CONSENSUS v2（终版）** |
|------|---------------|-------------|----------------------|
| 字面量语法 | 预留阶段 2 | 不需要 | **不需要** |
| 存储时区 | 建议 UTC | 确认 UTC | **确认 UTC** |
| 算术运算 | 不推荐 AddOp/SubOp | 仅有内置函数 | **操作符 `+` `-` 实现（C# 语义）** |
| 时间差值 | NumberValue\<double\> | NumberValue\<double\> | **TimeSpanValue（强类型）** |
| 内置函数 | date_add/date_sub/date_diff | date_add/date_sub/date_diff | **仅 timespan(value, unit)（操作符取代其他）** |
| Prototype | 简要提及 | DateTimePrototype (10 属性 + toString) | **DateTimePrototype + TimeSpanPrototype（各 10+ 属性）** |
| JSON 序列化 | 待确认 | ISO 8601 | **ISO 8601 + TimeSpan 标准格式** |
| 新增类型 | 1 (DateTimeValue) | 1 (DateTimeValue) | **2 (DateTimeValue + TimeSpanValue)** |
| 新增文件 | 2 | 2 | **4** |
