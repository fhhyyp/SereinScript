# SereinScript AST 编译器（Compiler）二次开发文档

> 本文档针对 `ScriptLang/Runtime/ByteCode/Compiler.cs` (1203行)，面向需要对 AST → 字节码编译流程进行二次开发的开发者。
> 关联文件：`OpCode.cs`, `ByteCodeChunk.cs`, `VariableTable.cs`, `VariableTableBuilder.cs`, `CaptureAnalysis.cs`, `GlobalSlotRegistry.cs`

---

## 1. 架构概览

```
┌───────────────┐     ┌─────────────────────┐     ┌──────────────────┐
│    AST        │ ──► │  Compiler.Compile()  │ ──► │  ByteCodeChunk   │
│  (ProgramExpr)│     │  Visit() + Emit()    │     │  (.Code + .VT    │
└───────────────┘     └─────────────────────┘     │   + .Constants)   │
                         │          ▲             └──────────────────┘
                         ▼          │
                   ┌──────────┐    │
                   │ Capture  │    │
                   │ Analysis │────┘   (双向反馈)
                   └──────────┘
```

编译器将 AST **单次遍历**编译为基于**槽位（Slot）的字节码**。核心创新是**全槽位化变量系统**——运行时零名字查找，所有变量通过槽位数组 O(1) 访问。

### 核心设计决策

| 决策 | 说明 |
|------|------|
| **全槽位化** | 编译时为所有变量分配常量槽位索引，运行时 O(1) 访问 |
| **两遍闭包编译** | 第一遍发现嵌套捕获，第二遍重新编译以正确注册捕获变量 |
| **原地运算优化** | `x = x + y` 模式编译为 `AddInPlace`，避免重新分配 NumberValue |
| **MutableNumber** | `var` 数值变量使用可变容器，算术运算零堆分配 |
| **延迟 Fixup** | 槽位指令先发占位符，Build 阶段统一修正运行时槽位索引 |
| **作用域栈** | 编译期用作用域栈追踪变量绑定，与运行时帧结构对应 |

---

## 2. 字节码指令集

### 2.1 完整 OpCode 枚举 (52 条指令)

```csharp
public enum OpCode : byte
{
    // ===== 常量加载 (7) =====
    LoadNull=0x01, LoadTrue=0x02, LoadFalse=0x03, LoadConst=0x04,
    LoadM1=0x93, Load0=0x94, Load1=0x95,

    // ===== 槽位操作 (2) =====
    LoadSlot=0x05, StoreSlot=0x08,

    // ===== 栈操作 (2) =====
    Pop=0x09, Dup=0x10,

    // ===== 数值/可变 (1) =====
    ToMutable=0x9F,

    // ===== 原地运算 (5) =====
    AddInPlace=0xA1, SubInPlace=0xA2, MulInPlace=0xA3,
    DivInPlace=0xA4, ModInPlace=0xA5,

    // ===== 算术 (6) =====
    Add=0x20, Sub=0x21, Mul=0x22, Div=0x23, Mod=0x24, Neg=0x25,

    // ===== 逻辑/比较 (8) =====
    Not=0x26, Equal=0x27, Ne=0x28, Gt=0x29, Ge=0x2A, Lt=0x2B, Le=0x2C,
    And=0x2D, Or=0x2E,

    // ===== 跳转 (3) =====
    Jmp=0x30, JumpIfTrue=0x31, JmpIfFalse=0x32,

    // ===== 函数/闭包 (3) =====
    CreateClosure=0x40, Call=0x41, Return=0x42,

    // ===== 模块 (1) =====
    Import=0x50,

    // ===== 对象 (3) =====
    CreateObject=0x70, GetMember=0x71, SetMember=0x72,

    // ===== 数组 (3) =====
    CreateArray=0x80, GetIndex=0x81, SetIndex=0x82,

    // ===== 迭代器 (4) =====
    GetIterator=0x90, MoveNext=0x91, Current=0x92, CurrentToSlot=0x96,

    Nop=0x00,
}
```

### 2.2 Instruction 结构

```csharp
public sealed record Instruction(OpCode OpCode, object? Operand = null);
```

操作数含义因指令而异：槽位索引 (`int`)、常量索引 (`int`)、参数数量 (`int`)、跳转目标 IP (`int`)、闭包元组等。

---

## 3. 编译时数据结构

### 3.1 Compiler 核心状态

```csharp
public sealed class Compiler
{
    // 输出
    private readonly List<Instruction> _code = [];     // 指令缓冲
    private readonly ByteCodeChunk _chunk = new();      // 最终产物
    private readonly VariableTableBuilder _varTable;     // 槽位分配器

    // 编译时状态
    private readonly Stack<Dictionary<string,VariableBinding>> _scopeStack;  // 作用域栈
    private readonly HashSet<string> _localNames;        // 局部变量名集合
    private HashSet<string>? _currentLambdaCaptures;     // 当前 Lambda 的捕获集合
    private readonly HashSet<string> _externalGlobals;   // 外部全局变量

    // 延迟处理
    private readonly List<(int index, SlotRegion, int)> _pendingSlotFixups;  // 待修正槽位
    private readonly HashSet<string> _placeholderCaptureNames;  // 占位捕获名
}
```

### 3.2 VariableBinding（编译时绑定）

```csharp
internal sealed class VariableBinding
{
    public int Slot { get; set; }        // 区域内的局部索引
    public bool IsMutable { get; set; }
    public bool IsCaptured { get; set; }
    public bool IsGlobal { get; set; }
    public bool IsImported { get; set; }
    public SlotRegion Region { get; set; }  // Local | Capture | Global | Builtin
}
```

### 3.3 槽位布局 (VariableTable)

```
┌──────────┬──────────┬──────────┬──────────┐
│  Local   │ Capture  │  Global  │ Builtin  │
│  Slots   │  Slots   │  Slots   │  Slots   │
│  [0..L)  │ [L..L+C) │[L+C..L+C+G)│[L+C+G..)│
└──────────┴──────────┴──────────┴──────────┘
  0      CaptureOffset  GlobalOffset  BuiltinOffset
```

每个帧的槽位数组按此布局组织，四种区域有独立的命名空间和分配策略：

| 区域 | 分配策略 | 共享范围 |
|------|----------|----------|
| Local | 单调递增，不回收 | 当前帧 |
| Capture | 按名去重 | 闭包链 |
| Global | 注册到 GlobalSlotRegistry | 全局 |
| Builtin | 按名去重 | 全局（VM 静态初始化） |

### 3.4 VariableTableBuilder

```csharp
public sealed class VariableTableBuilder
{
    public int AllocLocal(string name, bool isParameter);   // 单调递增，不去重
    public int AllocCapture(string name);                   // 按名去重
    public void FreeCapture(string name);                   // 影子变量时释放
    public int AllocGlobal(string name);                    // 注册到 GlobalSlotRegistry
    public int AllocBuiltin(string name);                   // 按名去重
    public VariableTable Build();                           // 构建最终不可变表
}
```

---

## 4. 编译流程 (Compile)

### 4.1 主入口

```csharp
public ByteCodeChunk Compile(Expr expr)
{
    Visit(expr);                        // 1. 递归遍历 AST，生成指令 + 分配槽位
    if (_code[^1].OpCode != Return)     // 2. 保证以 Return 结尾
        Emit(Return);
    _chunk.Code.AddRange(_code);        // 3. 将指令写入 Chunk
    var vt = _varTable.Build();         // 4. 构建 Slot → Region 映射
    _chunk.VariableTable = vt;
    FixupSlots(vt);                     // 5. 修正所有 LoadSlot/StoreSlot 中的槽位索引
    return _chunk;
}
```

### 4.2 FixupSlots — 运行时槽位地址解析

编译时发射的是**区域 + 区域本地索引**，Build 后统一转换为**全局槽位索引**：

```csharp
private void FixupSlots(VariableTable vt)
{
    foreach (var (index, region, localSlot) in _pendingSlotFixups)
    {
        int runtimeSlot = region switch
        {
            Local   → localSlot                        // [0, L)
            Capture → vt.CaptureOffset + localSlot     // [L, L+C)
            Global  → vt.GlobalOffset + localSlot      // [L+C, L+C+G)
            Builtin → vt.BuiltinOffset + localSlot     // [L+C+G, Total)
        };
        _chunk.Code[index] = new Instruction(..., runtimeSlot);
    }
}
```

### 4.3 变量解析链 (ResolveVariable)

```csharp
private VariableBinding? ResolveVariable(string name)
{
    // 1. 搜索作用域栈（从顶到底）
    foreach (var scope in _scopeStack.Reverse())
        if (scope.TryGetValue(name, out var binding))
            return binding;  // 如果是闭包内引用外层变量，标记 _currentLambdaCaptures

    // 2. 外部全局变量
    if (_externalGlobals.Contains(name))
        return new VariableBinding(AllocGlobal(name), true) { Region=Global };

    // 3. GlobalSlotRegistry（import 预注册）
    if (GlobalSlotRegistry.IsRegistered(name))
        return new VariableBinding(AllocGlobal(name), true) { Region=Global };

    // 4. 内置函数
    if (BuiltinFunctions.FunctionCaches.Any(f => f.Name == name))
        return new VariableBinding(AllocBuiltin(name), false) { Region=Builtin };

    return null;  // 未定义变量 → 抛出异常
}
```

---

## 5. 各 AST 节点编译策略

### 5.1 字面量 (CompileLiteral)

使用专用快捷指令优化高频值：

```csharp
null  → LoadNull    true → LoadTrue    false → LoadFalse
-1    → LoadM1      0   → Load0        1     → Load1
其他  → LoadConst(index)   // 常数表索引（带 -128~127 紧凑编码）
```

### 5.2 二元运算 (CompileBinary)

**短路求值** (`&&` / `||`) 生成跳转逻辑：

```csharp
// a && b 的编译结果:
//   Visit(a)           # Push a
//   Dup                # 复制一份给 JmpIfFalse Pop
//   JmpIfFalse L1      # 如果是假跳转到 L1（栈顶是复制的 a）
//   Pop                # 弹掉原来的 a
//   Visit(b)           # Push b
//   Jmp L2
// L1:
//   Pop                # 弹掉复制的 a（原来的 a 仍在栈顶）
// L2:
```

```csharp
// a || b 的编译结果:
//   Visit(a)           # Push a
//   Dup
//   JmpIfTrue L1       # 如果是真跳转到 L1
//   Pop
//   Visit(b)
//   Jmp L2
// L1:
//   Pop
// L2:
```

**普通运算**：
```csharp
Visit(Left); Visit(Right); Emit(OpCode);
```

### 5.3 原地运算优化 (CompileAssign)

检测 `x = x op y` 模式：

```csharp
if (expr.Value is BinaryExpr bin
    && bin.Left is IdentifierExpr id
    && id.Name == expr.Name
    && IsArithmeticOp(bin.Op))
{
    Visit(bin.Right);                    // Push right
    // 编译时发射 AddInPlace slot, -1（占位符）
    EmitInPlaceOp(slot, inPlaceOp);
    _lastAssignWasInPlace = true;        // 不需要 Pop
    return;
}
```

- `x = x + y` → `AddInPlace`
- `x = x - y` → `SubInPlace`
- `x = x * y` → `MulInPlace`
- `x = x / y` → `DivInPlace`
- `x = x % y` → `ModInPlace`

### 5.4 Var 与 MutableNumber

```csharp
// CompileVar 在检测到初始值是数值时插入 ToMutable 指令：
if (IsNumericInit(expr.Value))
    Emit(OpCode.ToMutable);
```

`ToMutable` 将栈顶的不可变 `NumberValue<T>` 转换为 `MutableNumber`，后续算术写入走原地更新路径。

### 5.5 If / When / For

```csharp
// If: 条件后 JmpIfFalse → Then 分支 → Jmp End → Else 分支
// When: 临时变量存值 → 逐子句比较 Equal + JmpIfFalse → 匹配时执行 body 后 Jmp End
// For:
//   GetIterator → loop: MoveNext → JmpIfFalse exit
//   → CurrentToSlot → Body → Jmp loop
//   exit: LoadNull
```

### 5.6 Lambda / 闭包 — 两遍编译

闭包编译是编译器最复杂的部分，使用**两遍编译**策略处理嵌套闭包：

```
第 1 步: CaptureAnalysis.Analyze(lambda, _localNames) → freeVars
第 2 步: 第一遍编译 (innerCompiler1) → closureChunk
         递归收集嵌套闭包中的 CaptureNames → allCaptureNames
第 3 步: 如果 nestedOnlyVars.Count > 0
         第二遍编译 (innerCompiler2, 预注册嵌套捕获变量为占位符) → closureChunk
第 4 步: 递归自引用检测 (如 let fact = (n) => ... fact(n-1))
第 5 步: 分配捕获槽位 (AllocCapture + ReplaceBindingInScope)
第 6 步: 发射 CreateClosure(chunkIndex, parameters, captureMappings)
```

#### CreateInnerCompiler — 子编译器创建

```csharp
private Compiler CreateInnerCompiler(LambdaExpr expr,
    HashSet<string> directFreeVars, HashSet<string>? nestedCaptureVars)
{
    var inner = new Compiler(_externalGlobals);
    // 注册 Lambda 参数
    foreach (var param in expr.Params)
        inner._varTable.AllocLocal(param, isParameter: true);
    // 注册直接捕获变量
    foreach (var name in directFreeVars)
        inner._varTable.AllocCapture(name);
    // 第二遍：预注册嵌套闭包需要的捕获变量
    if (nestedCaptureVars != null)
        foreach (var name in nestedCaptureVars)
            inner._varTable.AllocCapture(name);  // 标记为占位符
    return inner;
}
```

#### 占位符机制 (PlaceholderCaptureNames)

嵌套捕获变量在第二遍被预注册为"占位符"。首次被 `CompileLet`/`CompileVar` 消费后，占位符被消费。后续同名变量（不同作用域、影子变量）创建独立的局部槽位。

### 5.7 Import

```csharp
private void CompileImport(ImportStmt expr)
{
    // 将 import 数据打包为 List<object?> 存入常量表
    var importData = new List<object?> { expr.FilePath };
    foreach (var (member, alias) in expr.Members)
    {
        importData.Add(member);
        importData.Add(alias);
    }
    int dataIndex = _chunk.AddConstant(importData);
    Emit(OpCode.Import, dataIndex);
    // 注册导入成员到作用域和 GlobalSlotRegistry
    foreach (var (member, alias) in expr.Members)
    {
        GlobalSlotRegistry.Register(alias ?? member);
        _varTable.AllocGlobal(alias ?? member);
    }
}
```

### 5.8 Block

代码块引入**新的作用域**，块内最后一条语句的值保留在栈顶：

```csharp
PushScope();
for (int i = 0; i < statements.Count; i++)
{
    Visit(statements[i]);
    if (i < statements.Count - 1)
        Emit(Pop);      // 中间语句的值丢弃
}
PopScope();
```

---

## 6. CaptureAnalysis（闭包捕获分析）

```csharp
public static HashSet<string> Analyze(LambdaExpr lambda, HashSet<string> localNames)
```

递归遍历 Lambda 体，收集所有自由变量：
- 遇到 `IdentifierExpr` → 如果不在 bound 集合中，加入 free 集合
- 遇到 `BlockExpr` → 按语句顺序扩展 bound（`let`/`var` 的声明名加入 bound）
- 遇到内层 `LambdaExpr` → 参数名加入 bound，与外部 bound 隔离
- 遇到 `ForExpr` → 循环变量加入 bound

返回的是候选捕获变量集，最终由 `ResolveVariable` 决定每个变量是局部、捕获、全局还是内置。

---

## 7. ByteCodeChunk 与常量编码

### 7.1 紧凑常量索引

```csharp
索引 [0]        → null
索引 [1]        → true
索引 [2]        → false
索引 [3..259]   → int -127..128
索引 [260..]    → 通用常量表 (_constants[i - 260])
```

`AddConstant()` 内部做去重，`GetConstant()` 按索引范围解码。

### 7.2 嵌套闭包存储

```csharp
public int RegisterClosure(ByteCodeChunk closureChunk)
{
    _closures.Add(closureChunk);
    return _closures.Count - 1;
}
```

`CreateClosure` 指令的操作数是 `(chunkIndex, parameters, captureMappings)` 元组。

---

## 8. 扩展指南

### 8.1 添加新指令

```csharp
// 1. OpCode.cs: 添加枚举值
NewOp = 0x60,

// 2. Compiler.cs: 添加编译方法
private void CompileNewExpr(NewExpr expr)
{
    Visit(expr.Target);
    Emit(OpCode.NewOp, expr.Operand);
}

// 3. VM.cs ExecuteInstruction(): 添加 case
case OpCode.NewOp:
    // ... 执行逻辑
    return true;
```

### 8.2 添加新的优化模式

```csharp
// 示例：优化常量化 x = 0 → Load0 + StoreSlot（避免常量表查询）
private void CompileAssign(AssignExpr expr)
{
    if (expr.Value is LiteralExpr lit && lit.Value is 0)
    {
        Emit(OpCode.Load0);
        EmitStoreSlot(binding);
        return;
    }
    // ... 原有逻辑
}
```

### 8.3 调试输出

编译器内置 DEBUG 追踪，在编译 Lambda 和闭包捕获时输出详细日志：

```csharp
#if DEBUG
Console.WriteLine($"[Compiler.CompileLambda] 参数: [{...}]");
Console.WriteLine($"[Compiler.CompileLambda] _localNames: [{...}]");
Console.WriteLine($"[CaptureAnalysis] 所有自由变量: [{...}]");
#endif
```

---

> **文档版本**: 1.0 | **对应源码**: `ScriptLang/Runtime/ByteCode/Compiler.cs` 等 | **最后更新**: 2026-06-06
