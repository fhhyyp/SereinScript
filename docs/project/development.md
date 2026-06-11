# 如何二次开发

本文档面向希望参与 SereinScript 开发或基于此项目进行二次开发的开发者。

---

## 开发环境搭建

### 必需工具

| 工具 | 版本 | 用途 |
|------|------|------|
| **.NET SDK** | 10.0+ | 编译和运行 |
| **Visual Studio 2022** / **Rider** / **VS Code** | 最新版 | IDE |
| **Node.js** | 18+ | VS Code 扩展开发（可选） |

### 克隆与构建

```bash
git clone https://github.com/yourusername/SereinScript.git
cd SereinScript/SereinScript
dotnet restore
dotnet build
```

### 解决方案结构

```
SereinScript.sln
├── ScriptLang                    # 核心库（net10.0）
├── ScriptLang.Generator          # 源码生成器（netstandard2.0）
├── ScriptLang.Lsp                # LSP 服务器（net10.0）
├── ScriptLang.Demo               # 命令行演示（net10.0）
└── ScriptAvaloniaApp             # 桌面应用（待完成）
```

---

## 项目架构分层

```
┌────────────────────────────────────────┐
│         ScriptLang.Demo / 宿主应用        │  ← 使用层
├────────────────────────────────────────┤
│          ScriptLang.Lsp                 │  ← 编辑器支持
├────────────────────────────────────────┤
│           ScriptLang (核心库)             │  ← 语言实现
│  ┌─────────┬──────────┬──────────────┐  │
│  │ Lexer   │ Parser   │ Runtime      │  │
│  │         │          │ ├─ VM        │  │
│  │         │          │ ├─ Scope     │  │
│  │         │          │ ├─ Value     │  │
│  │         │          │ ├─ Prototype │  │
│  │         │          │ ├─ Compiler  │  │
│  │         │          │ └─ System    │  │
│  └─────────┴──────────┴──────────────┘  │
├────────────────────────────────────────┤
│       ScriptLang.Generator              │  ← 编译期代码生成
└────────────────────────────────────────┘
```

---

## 扩展指南

### 场景一：添加新的语法结构

以添加「后置自增运算符 `++`」为例：

#### 1. 词法扩展

在 [Lexer/TokenType.cs](https://github.com/yourusername/SereinScript/tree/master/ScriptLang/Lexer/TokenType.cs) 中添加新 Token 类型:

```csharp
// TokenType.cs
public enum TokenType
{
    // ... 现有类型
    PlusPlus,   // ++
}
```

在 [Lexer/Lexer.cs](https://github.com/yourusername/SereinScript/tree/master/ScriptLang/Lexer/Lexer.cs) 中识别新 Token:

```csharp
case '+':
    if (Match('+')) { AddToken(TokenType.PlusPlus); break; }
    AddToken(TokenType.Plus); break;
```

#### 2. 语法扩展

在 [Parser/Parser.cs](https://github.com/yourusername/SereinScript/tree/master/ScriptLang/Parser/Parser.cs) 中为新 Token 注册解析函数:

```csharp
// 在 Pratt Parser 中注册前缀/后缀解析
RegisterPostfix(TokenType.PlusPlus, Precedence.Postfix);  // 后置 ++
```

#### 3. AST 节点扩展

在 [Parser/Ast.cs](https://github.com/yourusername/SereinScript/tree/master/ScriptLang/Parser/Ast.cs) 中添加新 AST 节点:

```csharp
public record PostfixIncrementExpr(Expr Target, SourceSpan SourceSpan) : Expr(SourceSpan);
```

#### 4. 编译器扩展

在 [Runtime/ByteCode/Compiler.cs](https://github.com/yourusername/SereinScript/tree/master/ScriptLang/Runtime/ByteCode/Compiler.cs) 中添加编译逻辑:

```csharp
// 处理 PostfixIncrementExpr
public override void Visit(PostfixIncrementExpr expr)
{
    Compile(expr.Target);
    // 生成增量指令...
}
```

#### 5. VM 扩展

在 [Runtime/ByteCode/OpCode.cs](https://github.com/yourusername/SereinScript/tree/master/ScriptLang/Runtime/ByteCode/OpCode.cs) 中添加新操作码（如需）。
在 [Runtime/ByteCode/VM.cs](https://github.com/yourusername/SereinScript/tree/master/ScriptLang/Runtime/ByteCode/VM.cs) 中添加新指令的执行逻辑。

#### 6. LSP 扩展

在 LSP Handlers 中为新语法节点添加补全、悬停等支持。

---

### 场景二：添加新的内置函数

以添加 `random(min, max)` 为例：

在 [ScriptLang/BuiltinCache.cs](https://github.com/yourusername/SereinScript/tree/master/ScriptLang/BuiltinCache.cs) 中注册:

```csharp
// BuiltinCache.cs
var randomFunc = FunctionValue.From("random", async args =>
{
    if (args.Count < 2)
        throw new RuntimeException("random() 需要 2 个参数");
    
    var min = Convert.ToDouble(((NumberValue)args[0]).Value);
    var max = Convert.ToDouble(((NumberValue)args[1]).Value);
    
    var random = new Random();
    return new NumberValue<double>(random.NextDouble() * (max - min) + min);
});

globalScope.Define("random", randomFunc);
```

同时更新 `SymbolTable` 的内置函数列表（用于 LSP 补全）。

---

### 场景三：添加新的系统模块

以添加一个 `math` 系统模块为例：

#### 1. 创建模块类

新建 [ScriptLang/System/MathModule.cs](https://github.com/yourusername/SereinScript/tree/master/ScriptLang/System/MathModule.cs):

```csharp
namespace ScriptLang.System;

public class MathModule : ScriptRuntimeObject
{
    public MathModule()
        : base("math")
    {
        AddFunction("abs", Abs);
        AddFunction("max", Max);
        AddFunction("min", Min);
    }

    private static Value Abs(FunctionValue self, Value[] args) { /* ... */ }
    private static Value Max(FunctionValue self, Value[] args) { /* ... */ }
    private static Value Min(FunctionValue self, Value[] args) { /* ... */ }
}
```

#### 2. 注册到全局作用域

在 `BuiltinCache` 或 `ScriptEngine` 中注册:

```csharp
globalScope.Define("math", new ClrObjectValue(new MathModule()));
```

#### 3. 更新 LSP

在 `SymbolTable.BuiltinNames` 中添加 `"math"`，在 `CompletionHandler` 的 import 补全列表中添加 `"math"`。

---

### 场景四：为值类型添加原型方法

使用源码生成器，编译期自动生成原型绑定代码：

```csharp
[PrototypeExtension(PushThis = true)]
public partial class MyPrototype
{
    // 原型属性
    [PrototypeProperty]
    public static int GetSize(StringValue target) => target.Value.Length;

    // 原型方法
    [PrototypeFunction]
    public static StringValue Repeat(StringValue target, NumberValue<int> count)
    {
        return new StringValue(string.Concat(Enumerable.Repeat(target.Value, count.Value)));
    }

    // 必须实现的分部方法——判定目标值是否匹配此原型
    public partial bool IsTarget(Value value) => value is StringValue;
}
```

编译后，生成器自动生成实现 `IPrototype` 接口的分部类，运行时通过 `PrototypeManager` 自动注册。

**关键属性**:
- `PrototypeExtensionAttribute.PushThis` — 是否将调用目标作为 `this` 参数传递给方法
- `PrototypeExtensionAttribute.NamingFormat` — API 命名风格（`Net` 首字母大写 / `Js` 首字母小写）
- `PrototypeFunctionAttribute.Name` — 自定义方法名（覆盖自动命名）
- `PrototypePropertyAttribute.Name` — 自定义属性名（覆盖自动命名）

---

### 场景五：扩展 LSP 功能

LSP 基于 [OmniSharp.Extensions.LanguageServer](https://github.com/OmniSharp/omnisharp-roslyn) 框架。

#### 添加新的 Handler

示例 — 添加 `RenameHandler`（重命名符号）:

1. 创建 [ScriptLang.Lsp/Handlers/RenameHandler.cs](https://github.com/yourusername/SereinScript/tree/master/ScriptLang.Lsp/Handlers/RenameHandler.cs):

```csharp
public sealed class RenameHandler : IRenameHandler
{
    private readonly WorkspaceManager _workspace;
    private readonly SymbolTable _symbolTable;

    public RenameHandler(WorkspaceManager workspace, SymbolTable symbolTable)
    {
        _workspace = workspace;
        _symbolTable = symbolTable;
    }

    // 实现 IRenameHandler 接口...
}
```

2. 在 [Program.cs](https://github.com/yourusername/SereinScript/tree/master/ScriptLang.Lsp/Program.cs) 中注册:

```csharp
var server = await LanguageServer.From(
    options => options
        // ... 现有 handlers
        .WithHandler<RenameHandler>()
);
```

#### 调试 LSP

LSP 服务器的日志输出到 stderr，可在 VS Code 的 Output 面板中查看（选择 "SereinScript" 通道）。

```csharp
Console.Error.WriteLine($"[LSP] debug info...");
```

---

## 调试指南

### 调试脚本执行

1. 在 `ScriptLang.Demo` 的 `Main` 中设置断点
2. 使用 `scirpt(page, index)` 辅助函数选择特定脚本
3. 在 `Program.cs` 中取消注释 `scirpt(2, 5)` 锁定目标脚本

### 调试字节码 VM

```csharp
// 在创建 ScriptEngine 时开启 VM 信息输出
var engine = new ScriptEngine();
// engine.IsPrintVMInfo = true;  // 输出执行过程
// engine.IsPrintInputSciptContent = true;  // 输出输入源码
```

### 调试源码生成器

1. 在 `GeneratorConfig.cs` 中将 `IsDebugScriptPrototypeToolkits` 设为 `true`
2. 构建时 Roslyn 会尝试附加调试器
3. 也可以在 VS 中将 `ScriptLang.csproj` 设置为启动项目，编译器分析器会自动加载

### 调试 LSP 服务器

1. 将 `ScriptLang.Lsp` 设置为启动项目
2. 配置命令行参数以使用 stdio 通信
3. 在 VS Code 扩展中配置 `sereinscript.server.path` 指向调试版本的 DLL

---

## 开发最佳实践

### 代码风格

- 遵循项目现有的 C# 命名规范
- 使用 `Nullable` 可空引用类型
- 为公开 API 添加 XML 文档注释
- 核心数据类使用 `record` 类型（不可变性）

### 核心设计原则

1. **不变性优先** — AST 节点、Value 类型优先设计为不可变
2. **编译期优于运行期** — 能用源码生成器的避免反射
3. **表达式驱动** — 新语法结构应设计为表达式，返回 Value
4. **渐进增强** — 新功能不完备时宁可降级也不要崩溃

### 性能考量

- 编译器缓存: AST → ByteCodeChunk 的编译结果应缓存
- 字节码持久化: 生产环境建议预编译为 `.ssc`
- 原型扩展: 使用源码生成器而非反射
- LSP: 使用增量更新，避免全量重解析

### 提交规范

```
<类型>: <简短描述>

类型:
- feat: 新功能
- fix: 问题修复
- refactor: 重构
- docs: 文档
- perf: 性能优化
- test: 测试
- lsp: 编辑器支持
```

---

## 现有开发文档

项目 `docs/` 目录下提供了详尽的技术设计文档：

| 文档 | 说明 |
|------|------|
| [词法分析器](../dev/reference/dev-lexer.md) | 词法分析器设计 |
| [语法分析器](../dev/reference/dev-parser.md) | 语法分析器设计（Pratt Parser） |
| [编译器](../dev/reference/dev-compiler.md) | 字节码编译器设计 |
| [虚拟机](../dev/reference/dev-vm.md) | 虚拟机执行引擎设计 |
| [运行时环境](../dev/reference/dev-runtime-environment.md) | 运行时环境设计 |
| [系统模块](system-modules.md) | 系统模块接口说明 |
| [语言参考手册](SereinScript-Language-Reference.md) | 完整语言参考手册 |
| [LSP 服务器](../dev/lsp/DESIGN_lsp.md) | LSP 服务器设计 |
| [字节码持久化](../dev/feature-bytecode-persistence/) | `.ssc` 字节码持久化设计文档集 |
