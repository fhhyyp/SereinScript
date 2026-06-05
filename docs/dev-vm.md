# SereinScript 虚拟机运行时（VM）二次开发文档

> 本文档针对 `ScriptLang/Runtime/ByteCode/VM.cs` (1573行)，面向需要理解或扩展字节码执行引擎的二次开发者。
> 关联文件：`CallFrame.cs`, `Value.cs`, `Scope.cs`, `ValueStack.cs`, `ICallable.cs`

---

## 1. 架构概览

```
┌──────────────────┐     ┌──────────────────────┐     ┌──────────┐
│   ByteCodeChunk  │ ──► │     VM.ExecuteAsync() │ ──► │  Value   │
│   (字节码 + 变量表)│     │  解释循环 + 栈帧管理   │     │ (运行时值)│
└──────────────────┘     └──────────────────────┘     └──────────┘
           │                        │
           ▼                        ▼
    ┌─────────────┐         ┌──────────────┐
    │ VariableTable│         │  CallFrame[] │
    │ (槽位布局)    │         │  (调用帧栈)   │
    └─────────────┘         └──────────────┘
                                    │
                              ┌──────────────┐
                              │ ValueStack   │
                              │ (操作数栈)    │
                              └──────────────┘
```

VM 是一个**栈式字节码解释器**，核心特点是**全槽位化变量系统**——运行时通过槽位数组 O(1) 访问所有变量，零字符串查找开销。

### 核心设计决策

| 决策 | 说明 |
|------|------|
| **栈式架构** | 所有运算通过操作数栈完成，指令隐式操作栈顶 |
| **槽位化帧** | 每帧有预分配的 `Value[] Slots`，不按名字查找 |
| **帧对象池** | `CallFramePool` 复用帧数组，减少 GC 压力 |
| **可控数值类型** | `MutableNumber` 支持原地更新，避免算术运算反复创建对象 |
| **异步支持** | `ExecuteInstruction` 返回 `Task<bool>`，原生函数可为异步 |
| **懒迭代器** | `RangeIterator` 按需生成 int，不预创建数组 |

---

## 2. 运行时类型系统 (Value)

### 2.1 继承体系

```
Value (abstract)
├── NullValue            — null
├── BoolValue            — true / false (单例)
├── StringValue          — 字符串
├── NumberValue<T>       — 数值: int, long, float, double, decimal
├── MutableNumber        — 可变数值（原地修改，零分配）
├── ArrayValue           — 数组
├── ObjectValue          — 对象 (Dictionary<string,Value>)
├── FunctionValue        — 原生函数
├── CompiledFunctionValue— 编译后的 DSL 函数
├── ClrObjectValue       — 包装 .NET 对象
├── ClrMethodValue       — 包装 .NET 方法
└── RangeIterator        — 惰性范围迭代器
```

### 2.2 类型检测

```csharp
public abstract class Value
{
    public virtual bool IsNumber => ...;       // 动态检查所有数值子类型
    public virtual bool IsNumber_Int => ...;
    public virtual bool IsNumber_Double => ...;
    public bool IsString => this is StringValue;
    public bool IsArray => this is ArrayValue;
    public bool IsFunction => this is FunctionValue;
    // ...
    public abstract T As<T>();                 // 泛型类型转换
    public string AsString() => ...;           // 快捷转换
    public bool AsBool() => ...;
}
```

### 2.3 NumberValue 缓存

```csharp
public static class NumberValueCache
{
    // 小整数缓存 -128..127
    public static readonly NumberValue<int>[] SmallIntegerCache;
    // 特殊值单例
    public static readonly NumberValue<double> NaN;
    public static readonly NumberValue<double> PositiveInfinity;
    public static readonly NumberValue<double> NegativeInfinity;
}
```

通过 `NumberValueFactory.Create(value)` 统一创建，自动命中缓存。

### 2.4 MutableNumber — 零分配可变数值

```csharp
public sealed class MutableNumber : Value
{
    internal NumberKind _kind;      // Int→Long→Float→Double→Decimal 升级链
    internal int _intValue;         // 联和存储，同一时刻仅一种类型有效
    internal long _longValue;
    internal float _floatValue;
    internal double _doubleValue;
    internal decimal _decimalValue;

    public void AddInPlace(Value other);   // 原地 +=
    public void SubInPlace(Value other);   // 原地 -=
    // ... MulInPlace, DivInPlace, ModInPlace
    public Value ToImmutable();            // 传参/返回时冻结为不可变
    public void SetFromInt(int value);     // 零分配设值（迭代器用）
    public void SetFrom(Value value);      // 用另一个值替换
}
```

类型升级链：`Int → Long → Float → Double → Decimal`，当右值类型精度更高时自动升级。

### 2.5 CompiledFunctionValue

```csharp
public class CompiledFunctionValue(
    List<string> parameters,            // 参数名列表
    ByteCodeChunk chunk,                // 函数体字节码
    VariableTable variableTable,        // 槽位布局
    LightweightClosure? closure)        // 闭包捕获
    : Value, ICallable
{
    // CallAsync 创建新 VM 实例执行:
    public async Task<Value> CallAsync(ScriptEngine engine, List<Value> args)
    {
        var vm = new VM(engine);
        return await vm.InvokeCompiledFunctionAsync(this, args);
    }
}
```

---

## 3. 调用帧 (CallFrame)

### 3.1 数据结构

```csharp
internal class CallFrame
{
    public ByteCodeChunk Chunk;       // 当前执行的代码块
    public int IP;                    // 指令指针
    public Value[] Slots;             // 统一槽位数组 [TotalCount]
    public VariableInfo[] Captures;   // 闭包捕获的 VariableCell 引用数组
}
```

### 3.2 帧池

```csharp
internal sealed class CallFramePool
{
    private readonly ConcurrentStack<CallFrame> _pool;
    public CallFrame Rent();          // 从池中获取
    public void Return(CallFrame);    // 归还（Reset 后入池）
}
```

`maxPoolSize = 32`，递归深度较浅时可消除所有帧分配开销。

### 3.3 帧初始化 (InitFrameSlots)

```csharp
private static void InitFrameSlots(CallFrame frame, LightweightClosure? closure)
{
    var vt = frame.Chunk.VariableTable;

    // 1. 捕获区: 从闭包的 VariableCell 引用取值
    if (closure != null)
        for (int i = 0; i < vt.CaptureCount; i++)
            frame.Slots[vt.CaptureOffset + i] = closure.CapturedCells[i].Cell.Value;

    // 2. 全局区: 从 GlobalSlotRegistry 取值
    for (int i = 0; i < vt.GlobalCount; i++)
        frame.Slots[vt.GlobalOffset + i] = GlobalSlotRegistry.GetValue(...);

    // 3. 内置函数区: 从 VM 静态数组取值
    for (int i = 0; i < vt.BuiltinCount; i++)
        frame.Slots[vt.BuiltinOffset + i] = _builtinValues[_builtinSlots[name]];
}
```

### 3.4 参数绑定 (BindParameters)

```csharp
private static void BindParameters(CallFrame frame, List<string> paramNames, List<Value> args)
{
    for (int i = 0; i < paramNames.Count; i++)
    {
        Value paramValue = i < args.Count ? args[i] : Value.Null;
        frame.Slots[vt.ParamSlots[name]] = paramValue;
        // 若参数被闭包捕获，也同步写入捕获区
        if (vt.CaptureNames.TryGetValue(name, out int capSlot))
            frame.Slots[vt.CaptureOffset + capSlot] = paramValue;
    }
}
```

---

## 4. 主执行循环

### 4.1 ExecuteAsync

```csharp
public async ValueTask<Value> ExecuteAsync(ByteCodeChunk chunk)
{
    _currentFrame = new CallFrame();       // 创建顶层帧
    _currentFrame.Init(chunk);
    InitFrameSlots(_currentFrame, null);   // 填充全局/内置区

    while (_currentFrame.IP < chunk.Code.Count)
    {
        var inst = chunk.Code[_currentFrame.IP];
        bool shouldContinue = await ExecuteInstruction(inst);

        if (!shouldContinue)               // Return 触发退出
            return _stack.Count > 0 ? Pop() : Value.Null;

        if (!IsControlFlowInstruction(inst.OpCode))
            _currentFrame.IP++;            // 非控制流指令自增 IP
    }
    return Pop() ?? Value.Null;
}
```

### 4.2 控制流指令

以下指令**自行管理 IP**，循环不自动递增：

```csharp
Jmp, JumpIfTrue, JmpIfFalse, Return, Call, MoveNext
```

---

## 5. 核心指令执行逻辑

### 5.1 槽位读写

```csharp
// LoadSlot slot → Push(Slots[slot])
case OpCode.LoadSlot:
    Push(_currentFrame.Slots[(int)inst.Operand!]);

// StoreSlot slot → Slots[slot] = Pop(); Push(Slots[slot])
case OpCode.StoreSlot:
    // 同步捕获区: 若 slot 属于 Capture 区，同步 VariableCell
    // 同步全局区: 若 slot 属于 Global 区，更新 GlobalSlotRegistry
    // 优化: MutableNumber 原地更新
```

### 5.2 二元运算分派

```csharp
private void BinaryOp(Func<Value, Value, Value> op)
{
    var right = Pop();
    var left = Pop();
    Push(op(left, right));
}

// AddOp 重载示例:
Value AddOp(Value l, Value r)
{
    if (l.IsNumber && r.IsNumber) → NumberOp (类型提升)
    if (l.IsString || r.IsString) → StringValue(l.AsString() + r.AsString())
    if (l is ArrayValue)         → ArrayValue(concat(l, r))
}
```

### 5.3 原地运算 (InPlaceOp)

```csharp
// x = x + y → AddInPlace slot
// 如果 Slots[slot] 是 MutableNumber，直接 .AddInPlace(right)
// 否则 Slots[slot] = fallbackOp(left, right)
// 同步捕获区/全局区的写操作
```

### 5.4 函数调用

```csharp
// Call argCount
case OpCode.Call:
    // 1. 从栈弹出 argCount 个参数（传参时 MutableNumber 冻结为不可变）
    // 2. 弹出函数对象
    // 3. 根据类型分发:
    //    - CompiledFunctionValue → CallCompiledFunction (推帧到 _frames)
    //    - FunctionValue (原生)    → func.CallAsync(engine, args)
    //    - ClrMethodValue          → 反射调用 CLR 方法
```

### 5.5 Return

```csharp
// Return → Pop() 返回值，冻结 MutableNumber
// 如果 _frames 为空 → 停止 VM
// 否则 pop 上一帧 → Push 返回值到栈 → 继续执行上一帧
```

### 5.6 闭包创建

```csharp
// CreateClosure (chunkIndex, params, captureMappings)
case OpCode.CreateClosure:
    // 1. 从 _currentFrame.Chunk 获取嵌套闭包 Chunk
    // 2. 遍历 captureMappings → 从外部帧 Capture 区获取值
    //    创建新的 VariableCell (每次实例化独立)
    //    回写外部帧的捕获区（供嵌套共享）
    // 3. Push CompiledFunctionValue(params, chunk, vt, closure)
```

### 5.7 数组/对象创建

```csharp
// CreateArray n → Pop n 次建 List<Value>
// CreateObject n → Pop n*2 次 (key,value 交替) 建 Dictionary<string,Value>
// GetMember → target.Pop(), name.Pop() → 查 ObjectValue / PrototypeManager / ClrObjectValue
// SetMember → value.Pop(), name.Pop(), target.Pop() → 写入
// GetIndex → index.Pop(), target.Pop() → arr[i] / str[i] / obj[key]
```

### 5.8 迭代器

```csharp
// GetIterator: iterable.Pop()
//   ArrayValue → _iteratorStack.Push(arr); Push(index=0); Push(true)
//   RangeIterator → _iteratorStack.Push(range); Push(true)

// MoveNext:
//   RangeIterator → range.MoveNext() → Push true/false
//   ArrayValue → index < arr.Count → Push true/false

// CurrentToSlot: 零分配写入槽位
//   RangeIterator → MutableNumber.SetFromInt(range.CurrentInt())
//   ArrayValue → Slots[slot] = arr[index]; index++
```

### 5.9 Import

```csharp
// Import dataIndex:
// 1. 从常量表获取 [filePath, member1, alias1, member2, alias2, ...]
// 2. 调用 _engine.ImportResolver.ResolveAsync(filePath) → ObjectValue exports
// 3. 将 export 成员值写入 GlobalSlotRegistry + 当前帧 Global 槽位区
```

### 5.10 短路求值兜底

`AndOp` 和 `OrOp` 是不经过编译器短路优化的兜底实现：

```csharp
void AndOp() { var r=Pop();var l=Pop();Push(BoolValue.Create(IsTrue(l)&&IsTrue(r))); }
void OrOp()  { var r=Pop();var l=Pop();Push(BoolValue.Create(IsTrue(l)||IsTrue(r))); }
```

---

## 6. 跨类型运算规则

### 6.1 数值类型提升 (NumberOp)

```
decimal 优先 → double 次之 → float 次之 → long 次之 → int 最末
```

```csharp
private static Value NumberOp(..., decOp, dblOp, fltOp, lngOp, intOp)
{
    if (l.IsDecimal || r.IsDecimal) → decOp
    if (l.IsDouble || r.IsDouble)   → dblOp
    if (l.IsFloat || r.IsFloat)     → fltOp
    if (l.IsLong || r.IsLong)       → lngOp
    return intOp
}
```

### 6.2 整除规则 (NumberOpDiv)

```csharp
// 整数除法始终升级到 double:
// decimal/decimal → decimal
// double/double   → double
// float/float     → float
// int/int         → double!  (5/2 = 2.5)
```

### 6.3 相等性比较 (IsEqual)

```csharp
// 数值 → CompareNumbers == 0
// null  → NullValue
// bool  → .Value == .Value
// string → .Value == .Value
// array → 逐元素 IsEqual
// object → 逐属性 IsEqual
// 其他 → .Equals
```

### 6.4 Truthiness (IsTrue)

```csharp
false  → NullValue, BoolValue{false}
true   → StringValue (非空), ArrayValue (非空), ObjectValue (非空)
false  → 其他
```

---

## 7. CLR 互操作

### 7.1 成员访问

```csharp
private static Value? AccessClrMember(ClrObjectValue clrObj, string memberName)
{
    // 1. 查找 PropertyInfo → 读取属性值
    // 2. 查找 MethodInfo   → 返回 ClrMethodValue
    // 3. 返回 null（由调用方报错或委托给 PrototypeManager）
}
```

### 7.2 方法调用

```csharp
// ClrMethodValue.InvokeAsync(args):
// 1. 通过 EmitHelper 创建动态委托
// 2. args 从 Value 转换为 CLR 类型
// 3. 调用委托
// 4. 返回值转换为 Value
```

### 7.3 类型转换映射

| Script Value | CLR Type |
|-------------|----------|
| `NullValue` | `null` |
| `StringValue` | `string` |
| `BoolValue` | `bool` |
| `NumberValue<int>` | `int` |
| `NumberValue<long>` | `long` |
| `NumberValue<float>` | `float` |
| `NumberValue<double>` | `double` |
| `ClrObjectValue` | 原始 `object` |

---

## 8. 扩展指南

### 8.1 添加新运行时类型

```csharp
// 1. Value.cs: 添加新 Value 子类
public class DateTimeValue(DateTime Value) : Value
{
    public DateTime Value { get; } = Value;
    public override T As<T>() { ... }
}

// 2. VM.cs: 在需要的地方处理新类型
case OpCode.GetMember:
    if (target is DateTimeValue dt && memberName == "year")
        Push(NumberValueFactory.Create(dt.Value.Year));
```

### 8.2 添加新指令语义

```csharp
// 1. OpCode.cs: 添加 OpCode
// 2. Compiler.cs: 添加生成逻辑
// 3. VM.cs ExecuteInstruction(): 添加 case

// 示例：添加 TypeOf 指令
case OpCode.TypeOf:
{
    var value = Pop();
    string typeStr = value switch
    {
        StringValue => "string",
        NumberValue<int> => "int",
        _ => "unknown"
    };
    Push(new StringValue(typeStr));
}
```

### 8.3 添加新运算重载

在对应的运算静态方法中增加类型分支：

```csharp
// 示例：支持 DateTime + TimeSpan
private static Value AddOp(Value left, Value right)
{
    // ... 原有逻辑 ...
    if (left is DateTimeValue dt && right is NumberValue<double> days)
    {
        return new DateTimeValue(dt.Value.AddDays(days.Value));
    }
}
```

---

> **文档版本**: 1.0 | **对应源码**: `ScriptLang/Runtime/ByteCode/VM.cs` 等 | **最后更新**: 2026-06-06
