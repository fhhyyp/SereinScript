# SereinDSL 项目文档

## 项目简介

SereinDSL 是一套基于 CLR 运行的自定义领域特定语言（DSL），旨在提供一种简洁、灵活的脚本语言，用于快速开发和执行各种任务。该 DSL 支持变量声明、表达式计算、控制流、函数、数组、对象等特性，并提供与 CLR 的无缝集成能力。


## 社群
QQ群 955830545
提供技术交流与支持，欢迎加入。
因为个人是社畜，所以可能不会及时回复，请谅解。

## 快速开始

1. **克隆仓库**：`git clone https://github.com/yourusername/SereinDSL.git`
2. **构建项目**：使用 Visual Studio 或 `dotnet build` 命令构建项目
3. **运行示例**：执行 `ScriptLang.exe` 命令运行默认示例，或指定示例脚本路径
4. **编写自定义脚本**：创建 `.script` 文件，编写自定义脚本代码
5. **执行自定义脚本**：使用 `ScriptLang.exe your-script.script` 命令执行自定义脚本

---

希望本文档能够帮助您了解和使用 SereinDSL。如果您有任何问题或建议，欢迎在 GitHub 仓库中提出 Issue 或 Pull Request。

## 项目架构

SereinDSL 项目采用经典的编译器前端架构，主要由以下核心模块组成：

### 1. 词法分析器（Lexer）

- 负责将源代码转换为 token 序列
- 支持基本语法元素的识别，如标识符、关键字、运算符、字面量等

### 2. 语法分析器（Parser）

- 基于 Pratt Parser 实现，负责将 token 序列转换为抽象语法树（AST）
- 支持各种表达式和语句的解析，如变量声明、赋值、函数定义、条件表达式、循环等

### 3. 运行时（Runtime）

- 解释执行抽象语法树
- 提供变量作用域管理、函数执行、类型转换等功能
- 支持与 CLR 对象的交互

### 4. 脚本引擎（ScriptEngine）

- 提供脚本加载和执行的入口点
- 管理模块导入和全局作用域

## DSL 核心概念

### 1. 值类型

- **基本类型**：数字（Number）、字符串（String）、布尔值（Bool）、空值（Null）
- **复合类型**：数组（Array）、对象（Object）、函数（Function）
- **CLR 类型**：通过 CLR 互操作支持的 .NET 类型

### 2. 作用域

- 全局作用域：所有脚本共享的顶级作用域
- 局部作用域：函数和代码块内的作用域
- 支持变量的定义、读取和修改

### 3. 模块系统

- 支持通过 `import` 语句导入其他脚本模块
- 支持选择性导入模块成员

### 4. CLR 互操作

- 支持访问 CLR 对象的属性和方法
- 支持将 CLR 对象作为变量注入到脚本中
- 支持基本类型和复合类型的双向转换

## DSL 语法介绍

### 1. 变量声明

SereinDSL 支持两种变量声明方式：

- **let**：声明不可变变量
- **var**：声明可变变量

```javascript
let a = 10           // 不可变变量
var b = 20           // 可变变量
b = b + 50           // 修改变量值
```

### 2. 表达式

#### 算术表达式

```javascript
let sum = a + b      // 加法
let difference = a - b  // 减法
let product = a * b    // 乘法
let quotient = a / b    // 除法
let remainder = a % b   // 取余
```

#### 字符串操作

```javascript
let name = "Hello"
let greeting = name + " World"  // 字符串拼接
let repeated = "abc" * 3        // 字符串重复
```

#### 逻辑表达式

```javascript
let flag = true
let flag2 = false
let result1 = flag && flag2  // 逻辑与
let result2 = flag || flag2  // 逻辑或
let result3 = !flag          // 逻辑非
```

### 3. 函数

SereinDSL 支持 Lambda 函数定义：

```javascript
let add = (a, b) => a + b
let result = add(5, 3)

let multiply = (x, y) => x * y
```

### 4. 数据结构

#### 数组

```javascript
let arr = [1, 2, 3, 4, 5]
let firstElement = arr[0]
let length = arr.length

// 数组拼接
let arr2 = arr + [6, 7, 8]
```

#### 对象

```javascript
let person = {
    name = "Alice",
    age = 30,
    active = true,
    emails = [
        "alice@google.com",
        "123456@qq.com"
    ]
}

let name = person.name
let firstEmail = person.emails[0]
```

### 5. 控制流

#### 条件表达式

```javascript
let num = 25
if num > 20 then {
    print("big")
} else {
    print("small")
}

// 三元表达式
let status = if num > 30 then "high" else "low"
```

#### 循环

```javascript
// 遍历数组
for i in [10, 20, 30] {
    print("item:", i)
}

// 遍历 CLR 集合
for hobby in person.Hobbies {
    print(hobby)
}
```

#### 模式匹配

```javascript
let value = 2
when value {
    1 => print("one"),
    2 => print("two"),
    3 => print("three"),
    _ => print("other")
}
```

### 6. 模块系统

```javascript
import { store } from "test-import.script"
import { add, mul } from "math.script"
```

## 使用方式

### 1. 命令行执行

```bash
# 编译后执行指定脚本
ScriptLang.exe ./Samples/1.1-基础运算.script
```

### 2. 编程方式执行

```csharp
using ScriptLang;

// 创建脚本引擎
var engine = new ScriptEngine();

// 执行脚本文件
var result = await engine.LoadAndRunAsync("./Samples/1.1-基础运算.script");

// 执行脚本代码
var code = "let a = 10; let b = 20; a + b";
var result2 = await engine.RunAsync(code, "<memory>");

// 注入 CLR 对象
var result3 = await engine.RunAsync(code, "<memory>", scope => {
    scope.Define("person", new ClrObjectValue(new TestPerson()));
});
```

## 示例说明

SereinDSL 项目提供了丰富的示例脚本，位于 `Samples` 目录下：

### 基础语法示例

- **1.1-基础运算.script**：演示基本算术运算
- **1.2-变量声明.script**：演示变量声明和修改
- **1.3-字符串操作.script**：演示字符串拼接和重复
- **1.4-函数.script**：演示 Lambda 函数定义和调用
- **1.5-对象.script**：演示对象创建和属性访问
- **1.6-数组.script**：演示数组创建和操作

### 高级语法示例

- **2.1-逻辑运算.script**：演示逻辑运算符的使用
- **2.2-条件表达式.script**：演示条件表达式和三元表达式
- **2.3-循环.script**：演示 for 循环的使用
- **2.4-模式匹配.script**：演示 when 模式匹配的使用

### CLR 交互示例

- **4.1-CLR对象.script**：演示与 CLR 对象的交互
- **4.2-异步调用.script**：演示异步方法调用
- **4.3-CLR回调.script**：演示 CLR 回调的使用

### LINQ 和高级功能示例

- **LINQ/**：演示类似 LINQ 的链式操作
- **高级-实现pinia/**：演示模块导入和复杂对象创建

## 运行时内置函数

### 1. 数组方法

SereinDSL 为数组提供了丰富的内置方法：

- **map**：映射数组元素
- **filter**：过滤数组元素
- **forEach**：遍历数组元素
- **slice**：截取数组
- **push**：添加元素到数组末尾
- **pop**：移除并返回数组末尾元素
- **reverse**：反转数组
- **find**：查找符合条件的元素
- **findIndex**：查找符合条件的元素索引

### 2. 字符串方法

- **length**：获取字符串长度
- **split**：分割字符串
- **substring**：截取字符串
- **toUpperCase**：转换为大写
- **toLowerCase**：转换为小写
- **trim**：去除首尾空白
- **contains**：检查字符串是否包含指定子串
- **startsWith**：检查字符串是否以指定子串开头
- **endsWith**：检查字符串是否以指定子串结尾

### 3. 对象方法

- **keys**：获取对象的所有键
- **values**：获取对象的所有值
- **has**：检查对象是否包含指定键

### 4. 类似 LINQ 的操作

通过自定义对象，可以实现类似 LINQ 的链式操作：

```javascript
let array = (arr) => {
    let value = arr
    var arrayObject = {
        value = value,
        select = (fn) => {
            var result = []
            for item in value {
                result = result + fn(item)
            }
            array(result)
        },
        where = (fn) => {
            var result = []
            for item in value {
                when fn(item) {
                    true => { result = result + item },
                    false => {}
                }
            }
            array(result)
        }
    }
}
```

## CLR 互操作

SereinDSL 提供了操作 CLR 对象的能力：

### 1. 属性访问

```javascript
print(person.Name)      // 访问 CLR 对象的属性
print(person.Age)
```

### 2. 方法调用

```javascript
print(person.Greet())    // 调用 CLR 对象的方法
print(person.AddYears(5))
```

### 3. 集合操作

```javascript
print(person.Hobbies)    // 访问 CLR 集合
print(person.Hobbies[0])  // 访问集合元素

// 遍历 CLR 集合
for hobby in person.Hobbies {
    print(hobby)
}
```

### 4. 类型转换

SereinDSL 自动处理脚本类型和 CLR 类型之间的转换：

```javascript
print(person.AddYears(10.5))  // 脚本传 double，CLR 方法接收 int
```

## 模块系统

SereinDSL 支持通过 `import` 语句导入其他脚本模块：

### 1. 导入模块成员

```javascript
import { store } from "test-import.script"
import { add, mul } from "math.script"
```

### 2. 模块导出

通过返回包含导出成员的对象来实现模块导出：

```javascript
// math.script
let add = (a, b) => a + b
let mul = (a, b) => a * b
{ add, mul }  // 导出 add 和 mul 函数
```
