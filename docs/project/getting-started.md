# 如何使用

本文档介绍 SereinScript 的安装、配置与使用方式。

---

## 环境要求

- **.NET SDK**: 10.0+
- **操作系统**: Windows / Linux / macOS
- **开发工具**（可选）: Visual Studio 2022+ / JetBrains Rider / VS Code

---

## 快速开始

### 1. 克隆仓库

```bash
git clone https://github.com/yourusername/SereinScript.git
cd SereinScript/SereinScript
```

### 2. 构建项目

```bash
dotnet build
```

构建产物:
- `ScriptLang.dll` — 核心库
- `ScriptLang.Demo.exe` — 命令行演示工具
- `ScriptLang.Lsp.exe` — LSP 语言服务器

### 3. 运行示例脚本

```bash
# 进入 Demo 项目输出目录
cd ScriptLang.Demo/bin/Debug/net10.0/

# 执行基础运算示例
dotnet ScriptLang.Demo.dll ./Samples/1/1.1-基础运算.script

# 或直接使用可执行文件
./ScriptLang.Demo ./Samples/1/1.1-基础运算.script
```

### 4. 编写第一个脚本

创建 `hello.script`:

```javascript
let name = "世界"
let greeting = "你好，" + name + "！"
print(greeting)

// 使用 Lambda
let add = (a, b) => a + b
print("1 + 2 = " + add(1, 2))

// 创建一个对象
let user = {
    name = "张三",
    age = 25,
    active = true
}
print("用户: " + user.name + ", 年龄: " + user.age)
```

运行:

```bash
./ScriptLang.Demo ./hello.script
```

---

## 编程方式嵌入

在您的 .NET 项目中添加 ScriptLang 引用，然后以编程方式执行脚本。

### 安装

在您的 `.csproj` 中添加项目引用:

```xml
<ItemGroup>
    <ProjectReference Include="..\ScriptLang\ScriptLang.csproj" />
</ItemGroup>
```

### 基础用法

```csharp
using ScriptLang;
using ScriptLang.Runtime;
using ScriptLang.Runtime.ByteCode;

// 1. 创建脚本引擎
var engine = new ScriptEngine();

// 2. 执行脚本文件
var task = engine.CreateTask("./hello.script");
var result = await task.RunAsync();
Console.WriteLine($"结果: {result}");

// 3. 执行脚本代码字符串
var code = "let a = 10; let b = 20; a + b";
var task2 = engine.CreateTask(code, "<memory>");
var result2 = await task2.RunAsync();
Console.WriteLine($"结果: {result2}");
```

### 注入 CLR 对象

```csharp
using ScriptLang.Runtime;

// 定义 CLR 类
public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Multiply(int a, int b) => a * b;
}

// 注入到脚本
var code = @"
    let calc = calculator
    let sum = calc.Add(10, 20)
    print(""10 + 20 = "" + sum)
";

var task = engine.CreateTask(code, "<memory>");
// 通过扩展作用域注入变量
await task.RunAsync();
```

### 编译与缓存

```csharp
using ScriptLang.Runtime.ByteCode;

// 编译脚本为字节码
var source = File.ReadAllText("script.script");
var lexer = new ScriptLang.Lexer.Lexer(source, "script.script");
var tokens = lexer.ScanTokens();
var parser = new ScriptLang.Parser.Parser(tokens, "script.script");
var ast = parser.Parse();

var compiler = new Compiler();
var chunk = compiler.Compile(ast);

// 保存为 .ssc 文件（可分发）
ByteCodeChunk.Save(chunk, "script.ssc");

// 后续加载执行（无需重新编译）
var loadedChunk = ByteCodeChunk.Load("script.ssc");
var task = engine.CreateTask(loadedChunk, "script.ssc");
await task.RunAsync();
```

### 批量编译

```bash
# 递归编译脚本及其所有 import 依赖
./ScriptLang.Demo --build ./Samples/高级/pinia/run-import.script
```

---

## 语言快速参考

### 变量声明

```javascript
let x = 10           // 不可变变量
var y = 20           // 可变变量
y = y + 5            // 修改可变变量
```

### 函数

```javascript
let add = (a, b) => a + b
let result = add(5, 3)

// 多行函数体
let greet = (name) => {
    let msg = "Hello, " + name
    print(msg)
}
```

### 条件表达式

```javascript
let score = 85
let grade = if score >= 90 then "A"
    else if score >= 80 then "B"
    else if score >= 70 then "C"
    else "D"

// 模式匹配
when score {
    90 => print("优秀"),
    80 => print("良好"),
    70 => print("中等"),
    _ => print("其他")
}
```

### 循环

```javascript
for item in [1, 2, 3, 4, 5] {
    print(item)
}
```

### 数据结构

```javascript
// 数组
let arr = [1, 2, 3]
arr.push(4)
arr.forEach((x) => print(x))

// 对象
let person = {
    name = "Alice",
    age = 30,
    greet = () => "Hi, " + person.name
}
```

### 模块导入

```javascript
// 从其他脚本导入
import { add, mul } from "math.script"

let result = add(1, mul(2, 3))
print(result)
```

---

## VS Code 扩展安装

SereinScript 提供 VS Code 扩展，支持语法高亮、代码补全、悬停提示等功能。

### 安装步骤

1. 进入 LSP 扩展目录:

```bash
cd ScriptLang.Lsp/lsp/
```

2. 安装依赖并打包:

```bash
npm install
npx vsce package
```

3. 在 VS Code 中安装生成的 `.vsix` 文件:

```bash
code --install-extension sereinscript-lsp-0.0.1.vsix
```

### 配置

在 VS Code `settings.json` 中配置 LSP 服务器路径:

```json
{
    "sereinscript.server.path": "path/to/ScriptLang.Lsp.dll"
}
```

### 扩展功能

| 功能 | 触发方式 |
|------|---------|
| **代码补全** | 输入时自动触发，`.` 成员补全 |
| **悬停提示** | 鼠标悬停在变量/函数上 |
| **跳转定义** | F12 或 Ctrl+Click |
| **查找引用** | Shift+F12 |
| **文档大纲** | Ctrl+Shift+O |

---

## 命令行参考

### ScriptLang.Demo 完整参数

```
用法:
  ScriptLang.Demo <script-path>            直接执行脚本
  ScriptLang.Demo --compare <script-path>  编译执行 vs 编译→保存→加载→执行 对比
  ScriptLang.Demo --save <script-path>     编译并保存为 .ssc 文件
  ScriptLang.Demo --load <ssc-path>        加载 .ssc 文件并执行
  ScriptLang.Demo --build <script-path>    递归编译脚本及其所有 import 依赖为 .ssc
```

### 性能对比

使用 `--compare` 模式可以验证直接编译执行与「编译→持久化→加载→执行」的结果一致性:

```bash
./ScriptLang.Demo --compare ./Samples/1/1.1-基础运算.script
```

---

## 完整语法参考

详细的语法说明、内置函数列表、CLR 互操作指南请参见 [语言参考手册](SereinScript-Language-Reference.md)。
