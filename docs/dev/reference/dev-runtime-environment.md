# SereinScript 运行环境与原型方法注入 二次开发文档

> 本文档针对脚本引擎的**运行环境层**——`ScriptEngine`、内置函数注册、模块导入、Scope 管理，以及**原型方法注入系统**——`IPrototype` 接口、`PrototypeManager`、源生成器。
> 源码位置：
> - 运行环境: `ScriptLang/ScriptEngine.cs`, `ScriptTask.cs`, `SourceManager.cs`, `Runtime/BuiltinFunctions.cs`, `Runtime/ImportResolver.cs`, `Runtime/Scope.cs`
> - 原型系统: `ScriptLang/Prototype/IPrototype.cs`, `PrototypeManager.cs`, `ArrayPrototype.cs`, `ObjectPrototype.cs`, `StringPrototype.cs`
> - 源生成器: `ScriptLang.Generator/ScriptPrototypeToolkits/`

---

## 1. 架构概览

```
                       ┌──────────────────────────┐
                       │     ScriptEngine          │
                       │  (引擎入口 + 流程编排)      │
                       └──────┬──────────┬────────┘
                              │          │
              ┌───────────────┤          ├───────────────┐
              ▼               ▼          ▼               ▼
    ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────────┐
    │  SourceManager│ │ ImportResolver│ │  Prototype   │ │ BuiltinFunctions │
    │  源码缓存     │ │  模块加载     │ │  Manager     │ │  内置函数         │
    └──────────────┘ └──────────────┘ └──────┬───────┘ └──────────────────┘
                                            │
                       ┌────────────────────┤
                       ▼                    ▼                    ▼
                 ┌──────────┐       ┌──────────┐        ┌──────────┐
                 │  Array   │       │  Object  │        │  String  │
                 │Prototype │       │Prototype │        │Prototype │
                 └──────────┘       └──────────┘        └──────────┘
                      ▲                  ▲                   ▲
                      └──────────────────┼───────────────────┘
                                         │
                              ┌──────────────────────┐
                              │  ScriptPrototypeGen  │
                              │  (源生成器, 编译时)    │
                              │  自动生成 IPrototype  │
                              │  接口实现代码          │
                              └──────────────────────┘
```

---

## 2. ScriptEngine — 引擎入口

### 2.1 生命周期

```csharp
public sealed class ScriptEngine
{
    public ImportResolver ImportResolver { get; }
    public SourceManager SourceManager { get; } = new();
    public Scope GlobalScope { get; } = new();
    public PrototypeManager PrototypeManager { get; }

    // 编译缓存: AST → ByteCodeChunk
    private readonly Dictionary<Expr, ByteCodeChunk> _compilationCache = [];

    public ScriptEngine()
    {
        ImportResolver = new ImportResolver(this);
        PrototypeManager = new PrototypeManager(this);
        // 注册三大原型
        PrototypeManager.Register<ArrayPrototype>();
        PrototypeManager.Register<ObjectPrototype>();
        PrototypeManager.Register<StringPrototype>();
    }
}
```

### 2.2 完整执行流程 (CreateTask)

```
ScriptEngine.CreateTask(filePath)
│
├─ 1. 源码加载
│   ├─ SourceManager.TryGetSource(filePath) → 缓存命中?
│   └─ 未命中 → File.ReadAllText(filePath) → SourceManager.AddSource()
│
├─ 2. 词法分析
│   └─ new Lexer(script, filePath).ScanTokens() → List<Token>
│
├─ 3. 语法分析
│   ├─ new Parser(tokens, filePath).Parse() → ProgramExpr
│   └─ 检查 parser.Diagnostics
│
├─ 4. 注册外部作用域
│   └─ RegisterExternalScopeToGlobalSlots(scope)
│
├─ 5. 编译 + 缓存
│   ├─ _compilationCache.TryGetValue(expr) → 命中?
│   └─ 未命中 → new Compiler().Compile(expr) → ByteCodeChunk
│
└─ 6. 创建 ScriptTask
    └─ factory = async () => { new VM(this).ExecuteAsync(chunk) }
```

### 2.3 ScriptTask

```csharp
public sealed class ScriptTask(Func<Task<Value>> task, CancellationTokenSource cts)
{
    public async Task<Value> RunAsync() => await task.Invoke();
    public void Cancel() => cts.Cancel();
    public CancellationToken Token => cts.Token;
}
```

`ScriptTask` 封装了延迟执行——同一 AST 编译一次，可通过 `RunAsync()` 多次执行（每次创建新 VM）。

### 2.4 全局变量注册

```csharp
// 编译前注册（供编译器识别）
ScriptEngine.RegisterGlobal("myGlobalVar");

// 运行时设置值（执行前）
ScriptEngine.SetGlobal("myGlobalVar", new StringValue("hello"));
```

---

## 3. 内置函数系统

### 3.1 注册机制

```csharp
public static class BuiltinFunctions
{
    public static List<FunctionValue> FunctionCaches { get; } = [
        debug, now, nowtime, sleep, typeof, print,
        range, len, keys, bool, int, double, str
    ];

    public static void RegisterAll(Scope scope)
    {
        foreach (var item in FunctionCaches)
            scope.DefineFunction(item);
    }
}
```

每次 `ScriptEngine.CreateTask()` 调用 `BuiltinFunctions.RegisterAll(GlobalScope)`。

### 3.2 内置函数清单

| 函数 | 签名 | 实现方式 |
|------|------|----------|
| `print` | `(args...)` | 同步原生 → 输出到 Console |
| `debug` | `(args...)` | 异步原生 → Debug 输出 |
| `typeof` | `(value)` | 同步原生 → 返回类型字符串 |
| `range` | `(start?, end)` | 同步原生 → 返回 `RangeIterator` |
| `now` | `()` | 同步原生 → `DateTime.Now.Ticks` |
| `nowtime` | `()` | 同步原生 → `DateTime.Now.ToString()` |
| `sleep` | `(ms)` | 异步原生 → `Task.Delay(ms)` |
| `len` | `(v)` | 同步原生 → 字符串/数组长度 |
| `keys` | `(obj)` | 同步原生 → 对象键数组 |
| `bool` | `(v)` | 同步原生 → 类型转换 |
| `int` | `(v)` | 同步原生 → 类型转换 |
| `double` | `(v)` | 同步原生 → 类型转换 |
| `str` | `(v)` | 同步原生 → 类型转换 |

### 3.3 添加内置函数

```csharp
// 1. 在 BuiltinFunctions 中声明:
private static readonly FunctionValue myFunc = new(nameof(myFunc), static args =>
{
    // 参数校验
    if (args.Count != 1) throw new RuntimeException("myFunc() 期望 1 个参数");
    // 实现逻辑
    return new StringValue($"result: {args[0].AsString()}");
});

// 2. 添加到 FunctionCaches:
public static List<FunctionValue> FunctionCaches { get; } = [
    // ... 已有 ...
    myFunc,
];

// 3. VM 静态构造函数中会自动注册到 _builtinValues
```

对于异步函数，使用 `new FunctionValue(name, async args => { ... })` 构造。

---

## 4. ImportResolver — 模块系统

### 4.1 解析流程

```csharp
public class ImportResolver(ScriptEngine engine)
{
    private readonly ConcurrentDictionary<string, ObjectValue> _moduleCache = new();
    public string RootPath { get; internal set; }

    public async Task<ObjectValue> ResolveAsync(string filePath, Scope? scope = null)
    {
        var fullPath = ResolveFilePath(filePath);   // 相对路径 → 绝对路径

        if (_moduleCache.TryGetValue(fullPath, out var cached))
            return cached;                           // 缓存命中

        // 执行模块脚本
        var result = await _engine.RunModuleAsync(fullPath, scope);
        var exports = ExtractExports(result);        // 提取 ObjectValue
        _moduleCache[fullPath] = exports;            // 缓存
        return exports;
    }
}
```

### 4.2 模块导出约定

模块脚本的最后一条表达式的值作为模块的导出。若值为 `ObjectValue`，其属性即为可导入成员；否则导出空对象。

```csharp
private static ObjectValue ExtractExports(Value result)
{
    if (result is ObjectValue obj) return obj;
    return new ObjectValue([]);
}
```

### 4.3 模块依赖解析

```js
// pinia.script:
return { createStore }

// test-import.script:
import { createStore } from "pinia.script"  // 相对路径
let store = createStore({ ... })
return { store }

// run-import.script:
import { store } from "test-import.script"
import { look } from "look.script"
```

依赖图：
```
run-import.script
├── look.script ──→ test-import.script ──→ pinia.script
└── test-import.script ─────────────────→ pinia.script (缓存命中)
```

### 4.4 路径解析

```csharp
private string ResolveFilePath(string filePath)
{
    if (Path.IsPathRooted(filePath))
        return Path.GetFullPath(filePath);
    return Path.GetFullPath(Path.Combine(RootPath, filePath));
}
```

`RootPath` 由 `ScriptEngine.CreateTask()` 根据入口文件路径自动设置。

---

## 5. Scope — 作用域系统

### 5.1 数据结构

```csharp
public class Scope(Scope? parent = null)
{
    private readonly Dictionary<string, VariableInfo> _variables = [];
    public Scope? Parent { get; }

    public Scope CreateChildScope() => new(this);

    public VariableInfo Define(string name, Value value, bool isMutable = true);
    public void DefineFunction(FunctionValue func);
    public void DefineClrObject(string name, object data, bool isMutable = true);
    public bool TryGetValue(string name, out VariableInfo? info);    // 递归查找
    public void Set(string name, Value value);                        // 递归查找并设置
    public bool Exists(string name);                                  // 递归检查
}
```

### 5.2 VariableCell / VariableInfo

```csharp
public sealed class VariableCell(Value value)
{
    public Value Value = value;         // 可变引用（闭包共享的关键）
}

public sealed class VariableInfo(VariableCell cell, bool isMutable, bool isCaptured = false)
{
    public VariableCell Cell { get; }
    public bool IsMutable { get; set; }
    public bool IsCaptured { get; set; }
}
```

> **注意**: `Scope` 主要用于保留的解释器环境。VM 模式下，编译后的字节码不使用 `Scope`，而是通过槽位数组 `Value[] Slots` 直接访问变量。

---

## 6. 原型方法注入系统

### 6.1 IPrototype 接口

```csharp
public interface IPrototype
{
    bool IsLoad { get; }                                      // 是否已初始化
    void Init();                                               // 初始化（注册方法到字典）
    bool IsTarget(Value value);                                // 判断值是否为该原型的目标类型
    Value? GetMethod(Value value, string methodName, ScriptEngine engine);  // 获取方法
}
```

### 6.2 PrototypeManager

```csharp
public class PrototypeManager
{
    public List<IPrototype> Prototypes { get; } = new();

    public void Register<T>() where T : IPrototype, new()
    {
        var prototype = new T();
        prototype.Init();
        Prototypes.Add(prototype);
    }

    public bool TryGetValue(Value target, string member, out Value? value)
    {
        foreach (var proto in Prototypes)
        {
            if (proto.IsTarget(target))
            {
                var result = proto.GetMethod(target, member, _engine);
                if (result != null) { value = result; return true; }
            }
        }
        value = null;
        return false;
    }
}
```

### 6.3 调用链路

```
VM.GetMember()
├─ target is ObjectValue → obj.TryGetValue(name)
├─ PrototypeManager.TryGetValue(target, name)  ← 此处调用原型
│   ├─ ArrayPrototype.IsTarget(target)? → GetMethod(target, name)
│   ├─ ObjectPrototype.IsTarget(target)? → GetMethod(target, name)
│   └─ StringPrototype.IsTarget(target)? → GetMethod(target, name)
└─ target is ClrObjectValue → AccessClrMember(clrObj, name)
```

### 6.4 三个内置原型

#### ArrayPrototype

| 方法/属性 | 类型 | 说明 |
|-----------|------|------|
| `count` | Property | 数组元素数量 |
| `length` | Property | 同 count |
| `add(item)` | Function | 添加元素 |
| `first()` | Function | 第一个元素 |
| `last()` | Function | 最后一个元素 |
| `select(fn)` | Function (async) | 映射选择 |
| `where(fn)` | Function (async) | 条件筛选 |
| `orderBy()` | Function | 升序排序 |
| `orderByDesc()` | Function | 降序排序 |
| `toList()` | Function | 复制为列表 |

#### ObjectPrototype

| 方法/属性 | 类型 | 说明 |
|-----------|------|------|
| `count` | Property | 属性数量 |
| `keys()` | Function | 返回键列表 |
| `values()` | Function | 返回值列表 |
| `has(key)` | Function | 判断键是否存在 |
| `get(key)` | Function | 获取值 |
| `set(key, value)` | Function | 设置值 |
| `containsKey(key)` | Function | 同 has |
| `remove(key)` | Function | 删除键 |
| `clear()` | Function | 清空 |

#### StringPrototype

| 方法/属性 | 类型 | 说明 |
|-----------|------|------|
| `length` | Property | 字符长度 |
| `toUpper()` | Function | 转大写 |
| `toLower()` | Function | 转小写 |
| `trim()` | Function | 去除首尾空白 |
| `split(sep)` | Function | 分割 |
| `substring(start, length?)` | Function | 子串 |
| `contains(value)` | Function | 包含判断 |

---

## 7. 源生成器（ScriptPrototypeGenerator）

### 7.1 工作流程

```
编译时 (C# Source Generator)
  ┌──────────────────────────────────────────────────────┐
  │  SyntaxProvider.CreateSyntaxProvider(                  │
  │    Predicate: 筛选 ClassDeclarationSyntax             │
  │    Transform: 检查 [PrototypeExtension] Attribute     │
  │  )                                                    │
  │  → GeneratorCode() 生成 partial class 的 IPrototype   │
  │    实现代码（Init 方法、方法注册字典）                   │
  └──────────────────────────────────────────────────────┘
                     ↓
            生成 XXX.g.cs 文件
```

### 7.2 Attribute 定义（编译时注入）

生成器通过 `RegisterPostInitializationOutput` 将以下 Attribute 注入编译：

```csharp
[AttributeUsage(AttributeTargets.Class)]
internal sealed class PrototypeExtensionAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
internal sealed class PrototypePropertyAttribute : Attribute
{
    public string? Name = default;  // 重新定义属性名（可覆盖 C# 方法名）
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class PrototypeFunctionAttribute : Attribute
{
    public string? Name = default;  // 重新定义方法名
}
```

### 7.3 标记 → 代码生成

**输入** (标记的 C# partial class)：

```csharp
[PrototypeExtension]
public partial class ArrayPrototype
{
    public partial bool IsTarget(Value value) { return value is ArrayValue; }

    [PrototypeProperty(Name = "length")]
    private static NumberValue<int> Length(ArrayValue array) { ... }

    [PrototypeFunction(Name = "add")]
    private static void Add(ArrayValue array, Value item) { ... }

    [PrototypeFunction(Name = "select")]
    private static async ValueTask<ArrayValue> Select(ArrayValue array, ICallable func, ScriptEngine engine) { ... }
}
```

**输出** (生成的 `ArrayPrototype.g.cs`)：

```csharp
partial class ArrayPrototype : global::ScriptLang.IPrototype
{
    private static Dictionary<string, Func<Value, ScriptEngine, Value>> _prototypeMethods = [];

    bool _isLoad = false;
    public bool IsLoad => _isLoad;
    public partial bool IsTarget(Value value);

    Value? IPrototype.GetMethod(Value value, string methodName, ScriptEngine engine)
        => _prototypeMethods.TryGetValue(methodName, out var func) ? func(value, engine) : null;

    void IPrototype.Init()
    {
        // 为每个 [PrototypeProperty] 生成:
        var _Length = new Func<Value, ScriptEngine, Value>((v, env) =>
        {
            var target = (global::ScriptLang.Runtime.ArrayValue)v;
            return Length(target);
        });
        _prototypeMethods.Add("length", _Length);

        // 为每个 [PrototypeFunction] 生成:
        var _Add = new Func<Value, ScriptEngine, Value>((v, env) =>
        {
            return new FunctionValue("add", args =>
            {
                if (args.Count != 1) throw new RuntimeException("add() 期望 1 个参数");
                if (args[0] is not Value arg0) throw ...;
                var target = (ArrayValue)v;
                Add(target, arg0);
                return Value.Null;
            });
        });
        _prototypeMethods.Add("add", _Add);

        // 异步方法同理，使用 async args => ...

        _isLoad = true;
    }
}
```

### 7.4 生成器代码结构

| 文件 | 职责 |
|------|------|
| `ScriptPrototypeGenerator.cs` | 增量生成器入口，Attribute 定义注入，Pipeline 配置 |
| `ScriptPrototypeExtension.cs` | 代码生成逻辑：`GeneratorClass`, `GenerateProperty`, `GenerateMethod` |
| `GeneratorConfig.cs` | 全局配置：Type 格式化、默认 using |
| `Models/ClassCache.cs` | 类分析缓存模型 |
| `Models/MemberCache.cs` | 成员方法缓存模型 |
| `Models/AttrCache.cs` | Attribute 缓存模型 |
| `Extensions/` | 辅助扩展方法 |

### 7.5 方法生成规则

1. **第一个参数**：必须是原型目标类型（如 `ArrayValue`、`ObjectValue`、`StringValue`）
2. **ScriptEngine 参数**：如果有 `ScriptEngine` 类型的参数，生成时它被标记为 `env`
3. **返回值**：
   - `void` → 生成 `return Value.Null`
   - `Task` / `ValueTask` (无返回值) → `await ...; return Value.Null`
   - `Task<T>` / `ValueTask<T>` → `var result = await ...; return result`
   - 同步非 void → `var result = ...; return result`
4. **参数类型校验**：为每个参数生成 `if (args[i] is not ExpectedType) throw RuntimeException`
5. **属性 vs 方法**：`[PrototypeProperty]` 直接调用目标方法；`[PrototypeFunction]` 包装为 `FunctionValue`

---

## 8. 扩展指南

### 8.1 添加新的原型扩展

```csharp
// 1. 新增文件 ScriptLang/Prototype/MyTypePrototype.cs
namespace ScriptLang.Prototype;

[PrototypeExtension]
public partial class MyTypePrototype
{
    public partial bool IsTarget(Value value)
    {
        return value is MyTypeValue;
    }

    [PrototypeProperty(Name = "size")]
    private static NumberValue<int> Size(MyTypeValue v)
    {
        return NumberValueFactory.Create(v.Size);
    }

    [PrototypeFunction(Name = "doSomething")]
    private static StringValue DoSomething(MyTypeValue v, StringValue arg)
    {
        return new StringValue(v.Process(arg.Value));
    }
}

// 2. ScriptEngine 构造函数中注册:
public ScriptEngine()
{
    // ... 已有注册 ...
    PrototypeManager.Register<MyTypePrototype>();
}
```

### 8.2 注入外部 CLR 对象

```csharp
// 在创建 ScriptEngine 后，执行前：
var scope = new Scope();
scope.DefineClrObject("myService", new MyService());
scope.Define("configValue", new StringValue("production"), isMutable: false);

// 或者通过全局注册：
ScriptEngine.RegisterGlobal("myService");
ScriptEngine.SetGlobal("myService", new ClrObjectValue(new MyService()));
```

### 8.3 自定义模块路径解析

```csharp
// 继承或替换 ImportResolver
public class CustomImportResolver(ScriptEngine engine) : ImportResolver(engine)
{
    // 覆写 ResolveAsync 实现自定义加载逻辑（如从数据库/嵌入资源加载）
}
```

### 8.4 调试开关

```csharp
// ScriptEngine 支持调试输出:
var engine = new ScriptEngine();
// IsPrintVMInfo       → 输出字节码清和变量表单
// IsPrintInputSciptContent → 输出加载的原始脚本
// 默认在 DEBUG 构建下开启
```

---

## 9. 数据流总结

```
┌─────────────────┐
│  .script 文件    │
└────────┬────────┘
         ▼
┌─────────────────┐     ScriptEngine.CreateTask()
│  SourceManager  │ ←── 源码缓存 + 文件系统
└────────┬────────┘
         ▼
┌─────────────────┐
│     Lexer       │ → List<Token> + Diagnostics
└────────┬────────┘
         ▼
┌─────────────────┐
│     Parser      │ → ProgramExpr (AST) + Diagnostics
└────────┬────────┘
         ▼
┌─────────────────┐     _compilationCache 缓存
│    Compiler     │ ←── BuiltinFunctions 引用解析
└────────┬────────┘ ←── GlobalSlotRegistry 全局槽位注册
         ▼              ←── CaptureAnalysis 闭包分析
┌─────────────────┐
│  ByteCodeChunk  │ → .Code, .VariableTable, .Constants
└────────┬────────┘
         ▼
┌─────────────────┐     ScriptTask.RunAsync()
│       VM        │ ←── PrototypeManager 成员查找
└────────┬────────┘ ←── ImportResolver 模块加载
         ▼              ←── BuiltinFunctions 内置函数
┌─────────────────┐
│     Value       │  最终返回值
└─────────────────┘
```

---

> **文档版本**: 1.0 | **对应源码**: `ScriptLang/ScriptEngine.cs`, `Prototype/`, `ScriptLang.Generator/` | **最后更新**: 2026-06-06
