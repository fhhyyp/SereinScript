# SereinScript 语言参考手册

> 基于 Lexer (`ScriptLang/Lexer/`) 与 Parser (`ScriptLang/Parser/`) 实现分析及 `ScriptLang.Demo/Samples/` 全部示例脚本整理。

---

## 目录

1. [概述](#1-概述)
2. [词法结构](#2-词法结构)
3. [数据类型与字面量](#3-数据类型与字面量)
4. [变量与赋值](#4-变量与赋值)
5. [表达式与运算符](#5-表达式与运算符)
6. [函数与 Lambda](#6-函数与-lambda)
7. [控制流](#7-控制流)
8. [数据结构](#8-数据结构)
9. [模块导入](#9-模块导入)
10. [内置函数](#10-内置函数)
11. [CLR 互操作](#11-clr-互操作)
12. [完整语法形式化定义](#12-完整语法形式化定义)

---

## 1. 概述

SereinScript 是一门基于 .NET 平台的 **表达式驱动** 动态脚本语言。它采用 **Pratt 解析器**实现，一切皆为表达式（Expression），没有传统的语句/表达式二分。

### 核心设计特点

| 特性 | 说明 |
|------|:-----|
| **表达式优先** | 所有语法结构都是表达式，具有返回值 |
| **不可变优先** | `let` 声明不可变绑定，`var` 声明可变绑定 |
| **函数一等公民** | Lambda 支持闭包、柯里化、高阶函数 |
| **安全空访问** | `?.` 操作符实现 null-safe 成员访问 |
| **模式匹配** | `when` 表达式支持多子句模式匹配 |
| **CLR 互操作** | 可直接访问 .NET 对象的属性与方法 |
| **模块系统** | `import ... from` 支持跨文件代码复用 |

### 编译/执行模型

```
源文件 → Lexer (词法分析) → Token列表 → Parser (语法分析) → AST (Expr树) → Compiler -> VM 
```

---

## 2. 词法结构

### 2.1 字符集与编码

- 源文件采用 UTF-8 编码
- 大小写敏感
- 标识符支持 Unicode 字母（通过 `char.IsLetter()`）

### 2.2 空白与换行

- 空格 `' '`、回车 `'\r'`、制表符 `'\t'` 被忽略
- 换行 `'\n'` 用于行号计数，不作为语句终止符

### 2.3 注释

仅支持单行注释，以 `//` 开头至行尾：

```js
// 这是注释
let x = 10  // 这也是注释
```

> **注意**: 不支持多行注释 `/* ... */`。

### 2.4 标识符

标识符规则：以字母 `[a-zA-Z]`、下划线 `_` 或 `@` 开头，后续字符可为字母、数字或下划线。

```
name  _private  @class  counter1  my_function
```

> `@` 前缀用于与 C# 关键字冲突的标识符。

### 2.5 关键字

以下为保留关键字，不可用做变量名：

| 关键字 | 用途 |
|--------|------|
| `let` | 不可变变量声明 |
| `var` | 可变变量声明 |
| `if` | 条件表达式 |
| `then` | if 条件分支引导 |
| `else` | if 条件备选分支 |
| `when` | 模式匹配表达式 |
| `for` | 循环遍历表达式 |
| `in` | for 循环遍历目标标记 |
| `return` | 返回值表达式 |
| `import` | 模块导入 |
| `from` | 模块路径标记 |
| `true` | 布尔真 |
| `false` | 布尔假 |
| `null` | 空值 |

### 2.6 运算符与分隔符

| Token | 类型 | 说明 |
|-------|------|------|
| `+` | 算术 | 加法 / 字符串拼接 / 数组拼接 |
| `-` | 算术 | 减法 / 一元取负 |
| `*` | 算术 | 乘法 / 字符串重复 |
| `/` | 算术 | 除法 |
| `%` | 算术 | 取模 |
| `==` | 比较 | 等于 |
| `!=` | 比较 | 不等于 |
| `<` | 比较 | 小于 |
| `<=` | 比较 | 小于等于 |
| `>` | 比较 | 大于 |
| `>=` | 比较 | 大于等于 |
| `!` | 逻辑 | 逻辑非 |
| `&&` | 逻辑 | 逻辑与 |
| `||` | 逻辑 | 逻辑或 |
| `=` | 赋值 | 赋值 / 对象属性定义 |
| `=>` / `->` | 函数 | Lambda 箭头 |
| `.` | 成员访问 | 属性/方法访问 |
| `?.` | 成员访问 | 安全空属性/方法访问 |
| `(` `)` | 分组 | 括号分组 / 函数调用参数 |
| `{` `}` | 分组 | 代码块 / 对象字面量 / import 列表 |
| `[` `]` | 分组 | 数组字面量 / 索引访问 |
| `,` | 分隔 | 参数/元素分隔 |
| `:` | 分隔 | import 别名定义 |

---

## 3. 数据类型与字面量

### 3.1 数值类型

SereinScript 支持多种数值字面量，通过后缀区分类型：

| 后缀 | 类型 | 示例 | 说明 |
|------|------|------|------|
| _(无后缀)_ | `int` → `long` → `double` | `42` | 自动推断：优先 int，超出范围时 long，超大回退到 double |
| `L` / `l` | `long` (Int64) | `42L`, `42l` | 长整型 |
| `F` / `f` | `float` (Single) | `3.14f` | 单精度浮点 |
| `D` / `d` | `double` (Double) | `3.14d`, `3.14` | 双精度浮点（默认小数类型） |
| `M` / `m` | `decimal` | `10.5m` | 高精度十进制 |

#### 进制表示

```js
0xFF     // 十六进制 → int 255
0b1010   // 二进制   → int 10
```

- 十六进制: `0x` / `0X` 前缀 + `[0-9a-fA-F]+`
- 二进制: `0b` / `0B` 前缀 + `[01]+`
- 十六进制和二进制也支持后缀（如 `0xFFL` → `long`）

### 3.2 字符串

字符串使用双引号 `"..."` 包裹，支持转义序列：

```js
"Hello World"
"line1\nline2"     // \n 换行
"tab\there"        // \t 制表符
"quote: \"text\""  // \" 双引号
"path\\to\\file"   // \\ 反斜杠
```

> **限制**: 字符串不可跨行，不支持的转义字符会被原样保留（作为 `\` + 字符两个字符）。

### 3.3 布尔值与空值

```js
true       // 布尔真
false      // 布尔假
null       // 空值
```

### 3.4 数组字面量

```js
[1, 2, 3, 4, 5]                    // 整数数组
["a", "b", "c"]                     // 字符串数组
[[1, 2], [3, 4]]                    // 嵌套数组（矩阵）
[                                   // 允许换行
    { type = "Button", text = "OK" },
    { type = "TextBlock", text = "Hi" }
]
```

### 3.5 对象字面量

使用 `{ key = value, ... }` 语法创建 Map 对象：

```js
let person = {
    name = "Alice",
    age = 30,
    active = true
}
```

**简写形式**: 当属性值与同名变量相同时可省略 `= value`：

```js
let name = "Bob"
let age = 25
let user = { name, age }   // 等价于 { name = name, age = age }
```

属性键支持标识符和字符串两种形式：

```js
{
    "key-with-dash" = "value",
    normalKey = 123
}
```

---

## 4. 变量与赋值

### 4.1 `let` 声明（不可变绑定）

```js
let x = 10        // 不可重新赋值
let add = (a, b) => a + b
```

`let` 创建的绑定不可被 `=` 重新赋值。但注意：若值为对象/数组，其内部属性/元素仍可修改。

### 4.2 `var` 声明（可变绑定）

```js
var count = 0     // 可以重新赋值
count = count + 1
count = 100
```

`var` 变量可以使用 `=` 多次赋值。

### 4.3 赋值目标

赋值语句 `=` 支持以下左值形式：

```js
// 变量赋值
x = 42

// 索引赋值
arr[0] = 100
arr[i + 1] = value

// 成员赋值
obj.name = "NewName"
obj?.child?.prop = value    // 安全空成员赋值
```

### 4.4 变量作用域

- 变量在声明所在的代码块（`{...}`）内可见
- Lambda 闭包捕获外部变量（闭包语义）
- 同作用域内 `let`/`var` 不可重复声明同名变量

---

## 5. 表达式与运算符

### 5.1 运算符优先级

从低到高（Pratt Parser 解析顺序）：

| 优先级 | 运算符 | 结合性 |
|--------|--------|--------|
| 1 (最低) | `=` (赋值) | 右结合 |
| 2 | `||` (逻辑或) | 左结合 |
| 3 | `&&` (逻辑与) | 左结合 |
| 4 | `==`, `!=` (相等) | 左结合 |
| 5 | `<`, `<=`, `>`, `>=` (比较) | 左结合 |
| 6 | `+`, `-` (加减) | 左结合 |
| 7 | `*`, `/`, `%` (乘除模) | 左结合 |
| 8 | `!`, `-` (一元) | 右结合 |
| 9 (最高) | 调用 / 索引 / 成员访问 | 左结合 |

### 5.2 算术运算

```js
10 + 5        // → 15
10 - 5        // → 5
10 * 5        // → 50
10 / 3        // → 3.333...
10 % 3        // → 1
-10           // → -10（一元取负）
```

### 5.3 字符串运算

```js
"Hello" + " World"   // → "Hello World"（拼接）
"abc" * 3            // → "abcabcabc"（重复）
```

### 5.4 数组运算

```js
[1, 2] + [3, 4]     // → [1, 2, 3, 4]（拼接）
```

### 5.5 比较运算

```js
a == b              // 等于
a != b              // 不等于
a < b               // 小于
a <= b              // 小于等于
a > b               // 大于
a >= b              // 大于等于
```

### 5.6 逻辑运算

```js
!flag               // 逻辑非
a && b              // 逻辑与（短路求值）
a || b              // 逻辑或（短路求值）
```

### 5.7 成员访问

```js
person.name                 // 属性访问
person?.address?.city       // 安全空属性访问（任一中间值为 null 则返回 null）
array.length                // 内置属性
"hello".toUpper()           // 方法调用
```

### 5.8 索引访问

```js
arr[0]              // 数组索引
arr[i + 1]          // 表达式索引
matrix[0][1]        // 嵌套索引
```

### 5.9 函数调用

```js
add(1, 2)                     // 普通调用
makeAdder(1)(2)(3)            // 链式调用（柯里化）
someFunction(arg1, arg2, arg3)  // 多参数调用
```

### 5.10 括号分组

```js
(a + b) * c        // 改变默认优先级
```

---

## 6. 函数与 Lambda

### 6.1 基本语法

Lambda 使用 `=>` 或 `->` 箭头：

```
(param1, param2, ...) => body
param => body            // 单参数省略括号
() => body               // 无参数
```

```js
let add = (a, b) => a + b
let greet = (name) => "Hello " + name
let pi = () => 3.14159
```

### 6.2 表达式体与块体

```js
// 表达式体（单行，隐式返回）
(a, b) => a + b

// 块体（多行，最后一个表达式为返回值）
(n) => {
    var result = 1
    for i in range(1, n + 1) {
        result = result * i
    }
    result           // 此行作为返回值
}
```

### 6.3 闭包

Lambda 自动捕获外部变量（闭包语义）：

```js
let makeCounter = () => {
    var count = 0
    () => {
        count = count + 1
        count
    }
}

let c1 = makeCounter()
c1()   // 1
c1()   // 2
```

### 6.4 柯里化

```js
let add = (x) => (y) => (z) => x + y + z
add(1)(2)(3)       // → 6

let add2 = add(10)(20)
add2(30)           // → 60
```

### 6.5 高阶函数

函数可作为参数和返回值：

```js
let map = (arr, fn) => {
    var result = []
    for item in arr {
        result = result + [fn(item)]
    }
    result
}

map([1, 2, 3, 4], x => x * x)   // → [1, 4, 9, 16]
```

### 6.6 对象方法

对象的属性值可以是函数（Lambda），模拟面向对象的方法调用：

```js
let ui = {
    type = "Button",
    onClick = () => counter.value = counter.value + 1
}
ui.onClick()
```

---

## 7. 控制流

### 7.1 If-Then-Else 表达式

`if` 是表达式，**始终有返回值**：

```js
// 形式一：含 then 关键字
if condition then thenExpr else elseExpr

// 形式二：省略 then（直接跟表达式或块）
if condition thenExpr else elseExpr

// 形式三：块体
if x > 0 then {
    print("positive")
    x
} else {
    print("non-positive")
    0
}
```

```js
// 三元风格
let status = if score > 60 then "pass" else "fail"

// 多分支 (else if)
if x > 0 then "positive"
else if x < 0 then "negative"
else "zero"
```

> **注意**: `else` 分支可省略，此时默认返回 `null`。

### 7.2 When 表达式（模式匹配）

使用 `when` 进行模式匹配，类似其他语言的 `match` / `switch`：

```js
when value {
    pattern1 => body1,        // 逗号分隔多个子句
    pattern2 => body2,
    _ => defaultBody           // _ 为通配符/其他分支
}
```

特性：
- 每个子句由 `pattern => body` 组成
- 子句间用逗号分隔
- `_` 或 Lambda 作为通配符匹配
- body 可以是表达式或块 `{...}`

```js
let value = 25
when value {
    1 => print("one"),
    2 => print("two"),
    _ => print("other")
}
```

```js
// 模式匹配与块体
when typeof(item) {
    "array" => {
        var items = []
        for sub in item { items = items + [toJson(sub)] }
        "[" + items.join(",") + "]"
    },
    "object" => { /* ... */ },
    _ => item.toString()
}
```

### 7.3 For 循环表达式

```js
for varName in iterable body
```

```js
// 遍历数组
for item in [10, 20, 30] {
    print("item:", item)
}

// 遍历 range
for i in range(0, 100) {
    sum = sum + i
}

// 遍历对象的键
for k in keys(obj) {
    print("key:", k)
}
```

> **注意**: `for` 是表达式，但其返回值通常为最后一个迭代的 body 值。

### 7.4 Return 表达式

```js
return expr       // 带返回值
return            // 返回 null
```

`return` 用于提前退出函数体。

### 7.5 代码块

代码块 `{ ... }` 是一个表达式，其值为最后一个表达式的值：

```js
{
    let a = 1
    let b = 2
    a + b       // 代码块的值 → 3
}
```

---

## 8. 数据结构

### 8.1 数组

```js
let arr = [1, 2, 3, 4, 5]

// 访问
arr[0]               // 1
arr.length           // 5

// 拼接
arr + [6, 7]         // [1, 2, 3, 4, 5, 6, 7]

// 索引赋值
arr[0] = 100
```

内置属性/方法：
- `.length` — 数组长度
- `.add(item)` — 添加元素
- `.count` — 元素数量

### 8.2 对象（Map/Dictionary）

```js
let obj = {
    key1 = "value1",
    key2 = 123,
    method = () => key1 + "!"
}

// 属性访问
obj.key1             // "value1"
obj["key2"]          // 123

// 深层访问
obj?.nested?.value   // 安全空访问

// 属性赋值
obj.key3 = "newValue"

// 方法调用
obj.method()
```

内置属性/方法：
- `.has(key)` — 判断键是否存在
- `.keys` — 返回键列表

---

## 9. 模块导入

### 9.1 语法

```js
import { member1, member2 } from "path/file.script"
import { member1 : alias1, member2 } from "relative/path.script"
```

- 文件路径使用字符串，相对于当前脚本位置解析
- 支持成员别名：`member : alias`（使用冒号）
- 源文件最后的返回值（通过 `return` 或顶层表达式）作为被导入的模块对象
- 被导入模块通过 `{ member1, member2 }` 解构获取成员

### 9.2 示例

**定义模块** (`pinia.script`):
```js
let createStore = (options) => {
    // ...
}
return { createStore }
```

**导入模块** (`test-import.script`):
```js
import { createStore } from "pinia.script"

let store = createStore({ /* ... */ })
```

**导入带别名** (`look.script`):
```js
import { store } from "test-import.script"

let look = () => {
    print("x: ", store.state.x)
}
return { look }
```

### 9.3 依赖图

```
run-import.script
  ├── look.script
  │     └── test-import.script
  │           └── pinia.script
  └── test-import.script  (直接导入)
```

---

## 10. 内置函数

以下内置函数在示例中被使用，运行时环境提供：

### 10.1 输出

| 函数 | 签名 | 说明 |
|------|------|------|
| `print` | `print(arg1, arg2, ...)` | 输出到控制台，支持多参数，自动空格分隔 |

### 10.2 类型判断与转换

| 函数 | 签名 | 说明 |
|------|------|------|
| `typeof` | `typeof(value)` | 返回值的类型字符串：`"number"`, `"string"`, `"bool"`, `"null"`, `"array"`, `"object"`, `"function"` 等 |
| `bool` | `bool(value)` | 转换为布尔值 |
| `double` | `double(value)` | 转换为 double |

### 10.3 迭代与遍历

| 函数 | 签名 | 说明 |
|------|------|------|
| `range` | `range(start, end)` | 生成从 start 到 end-1 的整数序列（用作 for 迭代） |
| `keys` | `keys(obj)` | 返回对象的所有键列表 |
| `len` | `len(collection)` | 返回集合的长度 |

### 10.4 时间

| 函数 | 签名 | 说明 |
|------|------|------|
| `now` | `now()` | 返回当前时间戳（单位：100ns ticks，即 DateTime.Ticks） |

### 10.5 对象/数组操作

| 函数/方法 | 说明 |
|-----------|------|
| `.split(delimiter)` | 字符串分割 |
| `.join(separator)` | 数组元素拼接为字符串 |
| `.toUpper()` | 字符串转大写 |
| `.toString()` | 转换为字符串表示 |
| `.has(key)` | 对象是否包含指定键 |
| `.add(item)` | 数组添加元素 |
| `.last()` | 获取数组最后一个元素 |
| `.toList()` | 转换为列表 |

---

## 11. CLR 互操作

SereinScript 嵌入 .NET 运行时，可直接与 CLR 对象交互：

```js
let person = new_Person()      // 创建 .NET 对象（具体工厂函数由宿主提供）

person.Name = "张三"           // CLR 属性写入
person.Age = 18
let name = person.Name          // CLR 属性读取
let nextAge = person.AddYears(1)  // CLR 方法调用
let greet = person.Greet()
```

---

## 12. 完整语法形式化定义

### 12.1 Token 类型枚举 (TokenType)

```
Token:
    LeftParen     →  (
    RightParen    →  )
    LeftBrace     →  {
    RightBrace    →  }
    LeftBracket   →  [
    RightBracket  →  ]
    Comma         →  ,
    Dot           →  .
    QuestionDot   →  ?.
    Colon         →  :
    Semicolon     →  ;

    Plus          →  +
    Minus         →  -
    Star          →  *
    Slash         →  /
    Percent       →  %

    Equal         →  =
    EqualEqual    →  ==
    BangEqual     →  !=

    Less          →  <
    LessEqual     →  <=
    Greater       →  >
    GreaterEqual  →  >=

    Bang          →  !
    And           →  &&
    Or            →  ||

    Arrow         →  =>  (或 ->)

    Let       →  let      关键字
    Var       →  var      关键字
    If        →  if       关键字
    Then      →  then     关键字
    Else      →  else     关键字
    When      →  when     关键字
    For       →  for      关键字
    In        →  in       关键字
    Return    →  return   关键字
    Import    →  import   关键字
    From      →  from     关键字

    Number_Int     →  整数字面量 (Int32)
    Number_Long    →  长整数字面量 (Int64)
    Number_Float   →  单精度浮点字面量 (Single)
    Number_Double  →  双精度浮点字面量 (Double/默认小数)
    Number_Decimal →  高精度十进制字面量 (Decimal)

    String    →  字符串字面量
    True      →  true
    False     →  false
    Null      →  null

    Identifier →  标识符

    EOF       →  文件结束
    Unknown   →  未知 token (错误)
```

### 12.2 表达式 AST 节点

```
Expr = 
    | ErrorExpr        (错误占位节点)
    | LiteralExpr      (字面量: int/long/float/double/decimal/string/bool/null)
    | IdentifierExpr   (标识符引用)
    | LetExpr          (let 声明: let name = Value)
    | VarExpr          (var 声明: var name = Value)
    | AssignExpr       (变量赋值: name = Value)
    | IndexAssignExpr  (索引赋值: Target[Index] = Value)
    | MemberAssignExpr (成员赋值: Target.Prop = Value)
    | BinaryExpr       (二元运算: Left Op Right)
    | UnaryExpr        (一元运算: Op Expr)
    | ConditionalExpr  (三元条件, 保留节点)
    | ReturnExpr       (返回: return Expr?)
    | IfExpr           (条件: if Cond then Then else Else)
    | WhenExpr         (模式匹配: when Value { clauses })
    | WhenClause       (when 子句: Pattern => Body)
    | ForExpr          (循环: for VarName in Iterable Body)
    | LambdaExpr       (Lambda: Params => Body)
    | CallExpr         (函数调用: Target(Args...))
    | BlockExpr        (代码块: { Statements... })
    | ArrayLiteralExpr (数组: [Elements...])
    | ObjectLiteralExpr(对象: { Properties... })
    | ObjectProperty   (对象属性: Key = Value)
    | MemberAccessExpr (成员访问: Target.Property)
    | IndexAccessExpr  (索引访问: Target[Index])
    | ImportStmt       (导入: import { members } from "path")
    | ProgramExpr      (程序根节点: Statements...)
```

### 12.3 完整 EBNF 语法

```ebnf
(* ===== 顶层 ===== *)
Program          := (Declaration | Expression)* EOF

(* ===== 声明 ===== *)
Declaration      := LetDeclaration
                  | VarDeclaration
                  | ImportDeclaration
                  | ReturnStatement

LetDeclaration   := "let" Identifier "=" Expression
VarDeclaration   := "var" Identifier "=" Expression
ReturnStatement  := "return" Expression?
ImportDeclaration := "import" "{" ImportMembers "}" "from" StringLiteral
ImportMembers    := ImportMember ("," ImportMember)*
ImportMember     := Identifier (":" Identifier)?

(* ===== 表达式（按优先级递降） ===== *)
Expression       := Assignment

Assignment       := Or ("=" Assignment)?
                  (* 左值限制：Identifier / IndexAccess / MemberAccess *)

Or               := And ("||" And)*
And              := Equality ("&&" Equality)*
Equality         := Comparison (("==" | "!=") Comparison)*
Comparison       := Term (("<" | "<=" | ">" | ">=") Term)*
Term             := Factor (("+" | "-") Factor)*
Factor           := Unary (("*" | "/" | "%") Unary)*
Unary            := ("!" | "-") Unary | Call

Call             := Primary
                    ( "(" Args? ")"               (* 函数调用 *)
                    | "[" Expression "]"           (* 索引访问 *)
                    | ("." | "?.") Identifier      (* 成员访问 *)
                    )*

(* ===== 原子表达式 ===== *)
Primary          := NumericLiteral
                  | StringLiteral
                  | "true" | "false" | "null"
                  | Identifier ( "=>" LambdaBody )?   (* 单参数 Lambda *)
                  | "(" LambdaOrGroup ")"
                  | "{" ObjectLiteral "}"
                  | "[" ArrayLiteral "]"
                  | "if" Expression ("then")? (Block | Expression)
                    ("else" (Block | Expression))?
                  | "when" Expression "{" WhenClauses "}"
                  | "for" Identifier "in" Expression (Block | Expression)

(* ===== Lambda ===== *)
LambdaOrGroup    := ")" "=>" LambdaBody           (* 无参数 Lambda *)
                  | Identifier ("," Identifier)* ")" "=>" LambdaBody  (* 多参数 Lambda *)
                  | Expression ")"                 (* 普通括号分组 *)

LambdaBody       := Block | Expression

(* ===== 数据结构 ===== *)
ArrayLiteral     := (Expression ("," Expression)*)?
ObjectLiteral    := (ObjectProperty ("," ObjectProperty)*)?
ObjectProperty   := (Identifier | StringLiteral) ("=" Expression)?

(* ===== 控制流 ===== *)
WhenClauses      := WhenClause ("," WhenClause)*
WhenClause       := Expression "=>" (Block | Expression)

Block            := "{" Statement* "}"
Statement        := Declaration | Expression

Args             := Expression ("," Expression)*

(* ===== 词法 ===== *)
NumericLiteral   := [0-9]+ ("." [0-9]+)? Suffix?
                  | "0x" [0-9a-fA-F]+ Suffix?
                  | "0b" [01]+ Suffix?
Suffix           := [lLfFdDmM]
StringLiteral    := "\"" (任何非引号字符 | 转义序列)* "\""
Identifier       := [a-zA-Z_@][a-zA-Z0-9_]*
```

---

## 附录 A: 示例索引

| 文件 | 涵盖特性 |
|------|----------|
| `1.1-基础运算.script` | 算术运算、字符串操作、内置方法 |
| `1.2-变量声明.script` | `var` 声明与变量重赋值 |
| `1.3-字符串操作.script` | 字符串拼接 `+`、重复 `*` |
| `1.4-函数.script` | Lambda 定义与调用 |
| `1.5-对象.script` | 对象字面量、属性访问、嵌套 |
| `1.6-复杂对象.script` | 对象方法、Lambda 属性、链式调用 |
| `1.7-数组.script` | 数组字面量、索引访问、拼接 |
| `1.8-数值数据类型.script` | 完整数值类型、跨类型比较 |
| `2.1-逻辑运算.script` | `&&`, `\|\|`, `!` |
| `2.2-条件表达式.script` | `if-then-else` 表达式风格 |
| `2.3-循环.script` | `for-in`、`range()`、`keys()` |
| `2.4-模式匹配.script` | `when` 表达式 |
| `3.1-闭包.script` | 闭包、柯里化、高阶闭包、动态函数 |
| `3.2-高阶函数.script` | 函数作为参数 |
| `3.3-递归.script` | 递归函数（阶乘） |
| `3.4-快速排序.script` | 递归、数组操作、算法实现 |
| `3.5-矩阵运算.script` | 嵌套数组与矩阵加法 |
| `4.1-CLR对象.script` | CLR 对象互操作 |
| `test-*.script` | 闭包递归、深层递归、极限递归、相互递归、栈深度测试、闭包内存 |
| `高级/linq/*.script` | 复杂对象、链式 API、Linq 风格实现 |
| `高级/pinia/*.script` | 模块导入、状态管理、闭包模式 |

---

## 附录 B: 与其他语言语法对比

| 特性 | SereinScript | JavaScript | C# | Python |
|------|-------------|------------|----|--------|
| 不可变声明 | `let x = 1` | `const x = 1` | `var` / `readonly` | _(无)_ |
| 可变声明 | `var x = 1` | `let x = 1` | _(类型声明)_ | `x = 1` |
| Lambda | `(a,b) => a+b` | `(a,b) => a+b` | `(a,b) => a+b` | `lambda a,b: a+b` |
| 字符串拼接 | `"a" + "b"` | `"a" + "b"` | `"a" + "b"` | `"a" + "b"` |
| 字符串重复 | `"a" * 3` | `"a".repeat(3)` | _(无内置)_ | `"a" * 3` |
| 模式匹配 | `when x { ... }` | `switch(x) {...}` | `x switch { ... }` | `match x: ...` |
| 安全空 | `obj?.prop?.val` | `obj?.prop?.val` | `obj?.prop?.val` | _(无内置)_ |
| 对象语法 | `{ key = val }` | `{ key: val }` | `new { Key = val }` | `{"key": val}` |
| 注释 | `//` | `//` / `/* */` | `//` / `/* */` | `#` |
| 语句分隔 | 换行 | 分号/换行 | 分号 | 换行 |
| 模块导入 | `import { x } from "f"` | `import { x } from "f"` | `using X;` | `from f import x` |

---

> **文档版本**: 1.0  
> **生成日期**: 2026-06-06  
> **基于源码**: `ScriptLang/Lexer/Lexer.cs`, `ScriptLang/Lexer/TokenType.cs`, `ScriptLang/Parser/Parser.cs`, `ScriptLang/Parser/Ast.cs`  
> **示例来源**: `ScriptLang.Demo/Samples/` (1-4 级 + test + 高级)
