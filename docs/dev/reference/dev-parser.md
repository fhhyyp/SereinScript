# SereinScript 语法解析器（Parser）二次开发文档

> 本文档针对 `ScriptLang/Parser/` 模块，面向需要扩展语法规则的二次开发者。
> 源码位置：`ScriptLang/Parser/Parser.cs` (1149行), `Ast.cs` (197行), `ParseException.cs`, `ParserExtension.cs`

---

## 1. 架构概览

```
┌──────────────┐     ┌───────────────────┐     ┌─────────────────┐
│  List<Token> │ ──► │  Parser.Parse()   │ ──► │  ProgramExpr    │
│  (词法输出)   │     │  Pratt Parsing    │     │  (AST 根节点)   │
└──────────────┘     └───────────────────┘     └─────────────────┘
                            │
                            ▼
                     List<ParseException>
                     (错误恢复 + 累积诊断)
```

Parser 采用 **Pratt 解析器（自顶向下算符优先）** 架构。它用 **单一递归下降方法链**（按优先级递降）处理所有表达式，无须 Parser Generator。

### 核心设计特点

| 特点 | 说明 |
|------|------|
| **表达式优先** | 所有语法结构皆为 `Expr`，包括声明、控制流、代码块 |
| **Pratt Parsing** | 按优先级递降链式解析，二进制运算符左结合、赋值右结合 |
| **错误恢复** | 遇到语法错误不崩溃，生成 `ErrorExpr` 占位节点并 `Synchronize()` |
| **Span 追踪** | 每个 AST 节点携带 `SourceSpan`，精确到行/列/偏移 |

---

## 2. AST 节点体系

### 2.1 基类

```csharp
public abstract record Expr(SourceSpan SourceSpan);
```

全部 25 种表达式节点均为 `record`（不可变、值相等），继承自 `Expr`。

### 2.2 完整节点分类

#### 字面量与引用
```
LiteralExpr      — 字面量 (object? Value)
IdentifierExpr   — 标识符引用 (string Name)
```

#### 声明
```
LetExpr          — let 声明 (Name, Value)
VarExpr          — var 声明 (Name, Value)
ImportStmt       — import 语句 (Members, FilePath)
```

#### 赋值
```
AssignExpr       — 变量赋值 (Name, Value)
IndexAssignExpr  — 索引赋值 (Target, Index, Value)
MemberAssignExpr — 成员赋值 (Target, Property, Value, SafeNull)
```

#### 运算符
```
BinaryExpr       — 二元运算 (Left, Op, Right)
UnaryExpr        — 一元运算 (Op, Expr)
ConditionalExpr  — 三元条件 (Cond, Then, Else)
```

#### 控制流
```
IfExpr           — 条件 (Cond, Then, Else)
WhenExpr         — 模式匹配 (Value, Clauses, OtherClause)
WhenClause       — 匹配子句 (Pattern, Body)
ForExpr          — 循环 (VarName, Iterable, Body)
ReturnExpr       — 返回 (Value?)
BlockExpr        — 代码块 (Statements)
```

#### 函数
```
LambdaExpr       — Lambda (Params, Body)
CallExpr         — 函数调用 (Target, Args)
```

#### 数据结构
```
ArrayLiteralExpr   — 数组 (Elements)
ObjectLiteralExpr  — 对象 (Properties)
ObjectProperty     — 对象属性 (Key, Value)
```

#### 访问
```
MemberAccessExpr  — 成员访问 (Target, Property, SafeNull)
IndexAccessExpr   — 索引访问 (Target, Index)
```

#### 元节点
```
ProgramExpr       — 程序根 (Statements)
ErrorExpr         — 错误占位 (Message)
```

### 2.3 SourceSpan

```csharp
public record SourceSpan(
    string FilePath,
    int Start,          // 起始字符偏移
    int Length,         // 跨度长度
    int StartLine,      // 起始行号
    int StartColumn,    // 起始列号
    int EndLine,        // 结束行号
    int EndColumn       // 结束列号
);
```

---

## 3. Pratt 解析器详解

### 3.1 优先级链

解析从 `ParseExpression()` 开始，沿优先级递降链（从低到高）层层委托：

```
ParseExpression     → ParseAssignment
ParseAssignment     → ParseOr           (=)
ParseOr             → ParseAnd          (||)
ParseAnd            → ParseEquality     (&&)
ParseEquality       → ParseComparison   (== !=)
ParseComparison     → ParseTerm         (< <= > >=)
ParseTerm           → ParseFactor       (+ -)
ParseFactor         → ParseUnary        (* / %)
ParseUnary          → ParseCall         (! - 一元)
ParseCall           → ParsePrimary      (调用/索引/成员)
ParsePrimary                            (原子表达式)
```

### 3.2 前缀/中缀处理模式

```csharp
// 每个优先级层使用 while (Match(...)) 处理左递归的中缀运算符：
private Expr ParseOr()
{
    Expr expr = ParseAnd();
    while (Match(TokenType.Or))       // 中缀解析
    {
        Token op = Previous();
        Expr right = ParseAnd();      // 递归回下一层
        expr = new BinaryExpr(expr, op.Lexeme, right, span);
    }
    return expr;
}
```

### 3.3 后置操作符 (ParseCall 层)

`ParseCall()` 用 `while(true)` 循环处理函数调用、索引、成员访问：

```csharp
private Expr ParseCall()
{
    Expr expr = ParsePrimary();
    while (true)
    {
        if (Match(LeftParen)) expr = ParseCallArguments(expr);     // f(args)
        else if (Match(LeftBracket)) expr = ParseIndexAccess(expr); // arr[i]
        else if (Match(Dot, QuestionDot)) expr = memberAccess(...); // obj.prop
        else break;
    }
    return expr;
}
```

---

## 4. 各语法结构解析详解

### 4.1 声明解析

```csharp
// let x = <expr>
private LetExpr ParseLetDeclaration()
{
    Token name = Consume(Identifier, "在 'let' 之后需要变量名");
    Consume(Equal, "在变量名之后需要 '='");
    Expr value = ParseExpression();
    return new LetExpr(name.Lexeme, value, span);
}
// var 同理（使用 VarExpr）
```

关键：`let` 和 `var` 语法完全一致，区别仅在于 AST 节点类型（`LetExpr` vs `VarExpr`），编译器根据节点类型决定可变性。

### 4.2 import 声明

```csharp
// import { member1, member2 : alias2 } from "path"
private ImportStmt ParseImportDeclaration()
{
    Consume(LeftBrace, ...);
    // 解析成员列表，支持 member:alias 语法
    do {
        Token member = Consume(Identifier, ...);
        Token? alias = Check(Colon) ? Consume(Identifier, ...) : null;
        members.Add((member.Lexeme, alias?.Lexeme));
    } while (Match(Comma));
    Consume(RightBrace, ...);
    Consume(From, ...);
    Token path = Consume(String, ...);
    return new ImportStmt(members, path.Lexeme, span);
}
```

### 4.3 Lambda 解析

Lambda 的识别需要上下文判断——`(` 开头的可能是分组表达式也可能是 Lambda：

```csharp
// 在 ParsePrimary():
if (Match(LeftParen))
{
    if (IsNextLambda()) return ParseLambda();  // (a,b) => ...
    else { Expr expr = ParseExpression(); Consume(RightParen); return expr; }
}

// 在 ParsePrimary() 中处理单参数 Lambda:
if (Match(Identifier) && Peek().Type == Arrow)
    return ParseLambda();
```

#### IsNextLambda() — 前瞻判断

```csharp
private bool IsNextLambda()
{
    // () => ...              → Lambda
    // (Identifier) => ...    → Lambda (单参)
    // (Identifier, ...) => ... → Lambda (多参)
    // 其他                     → 普通分组
    int save = _current;
    // ... 解析参数列表，检查是否以 ) => 结尾
    _current = save;
    return isLambda;
}
```

### 4.4 If 表达式

```csharp
private IfExpr ParseIfExpression()
{
    Expr condition = ParseExpression();
    Expr thenBranch = Match(Then) ? ParseBlockOrExpr() : ParseBlockOrExpr(); // then 可选
    Expr elseBranch = Match(Else) ? ParseBlockOrExpr() : new LiteralExpr(null, ...);
    return new IfExpr(condition, thenBranch, elseBranch, span);
}
```

- `then` 关键字**可选省略**
- `else` 分支**可选省略**（默认返回 null）

### 4.5 When 表达式

```csharp
private WhenExpr ParseWhenExpression()
{
    Expr value = ParseExpression();
    // when value { pattern1 => body1, pattern2 => body2, _ => default }
    Consume(LeftBrace, ...);
    while (!Check(RightBrace))
    {
        Expr pattern = ParseExpression();
        // 特殊处理：_ 通配符 → Lambda 作为 OtherClause
        if (pattern is LambdaExpr lambda) { otherClause = ...; break; }
        Consume(Arrow, ...);
        Expr body = ParseBlockOrExpr();
        clauses.Add(new WhenClause(pattern, body, span));
        Match(Comma); // 逗号分隔
    }
    Consume(RightBrace, ...);
    return new WhenExpr(value, clauses, otherClause, span);
}
```

**通配符处理**: 当 `_ =>` 被解析为单参数 Lambda 时，它作为 `OtherClause` 存储。

### 4.6 For 循环

```csharp
private ForExpr ParseForExpression()
{
    string varName = Consume(Identifier, ...).Lexeme;
    Consume(In, ...);
    Expr iterable = ParseExpression();
    Expr body = Check(LeftBrace) ? ParseBlock() : ParseExpression();
    return new ForExpr(varName, iterable, body, span);
}
```

### 4.7 对象字面量

```csharp
private ObjectLiteralExpr ParseObjectLiteral()
{
    if (!Check(RightBrace))
    {
        do {
            string key = Check(Identifier) ? Advance().Lexeme : (string)Advance().Literal!;
            if (Match(Equal))
            {
                Expr value = ParseExpression();
                properties.Add(new ObjectProperty(key, value, span));
            }
            else
            {
                // 简写形式: { a } 等价于 { a = a }
                properties.Add(new ObjectProperty(key, new IdentifierExpr(key, ...), span));
            }
        } while (Match(Comma));
    }
    Consume(RightBrace, ...);
    return new ObjectLiteralExpr(properties, span);
}
```

### 4.8 程序根节点

```csharp
public Expr Parse()
{
    var statements = new List<Expr>();
    while (!IsAtEnd())
        statements.Add(ParseDeclarationOrStatement());
    return new ProgramExpr(statements, sourceSpan);
}
```

每个顶层项都尝试匹配声明（`let`, `var`, `import`, `return`）→ 否则按表达式处理。

---

## 5. 错误处理与恢复

### 5.1 ParseException

```csharp
private static ParseException Error(Token token, string message)
{
    // 生成： "{message}。 找到 '{token.Lexeme}' (类型: {token.Type})
    //        [文件 {token.FilePath} 第 {token.Line} 行, 第 {token.Column} 列]"
}
```

### 5.2 错误恢复 (Synchronize)

```csharp
private void Synchronize()
{
    Advance();
    while (!IsAtEnd())
    {
        if (Previous().Type == Semicolon) return;
        switch (Peek().Type)
        {
            case Let: case Var: case If: case For: case Return: case Import:
                return;  // 遇到语句边界关键字，停止跳过
        }
        Advance();
    }
}
```

策略：跳过后续 Token 直到遇到**语句边界**（`let`/`var`/`if`/`for`/`return`/`import`）或分号。

### 5.3 Consume() — 弹性消费

```csharp
private Token Consume(TokenType type, string message)
{
    if (Check(type)) return Advance();     // 正常消费
    var error = Error(Peek(), message);
    Diagnostics.Add(error);
    if (IsRecoverableBoundary(Peek().Type)) return Peek();  // 不消耗边界 Token
    Advance();                              // 消耗一个 Token 避免死循环
    return Previous();
}
```

边界 Token（不会在错误恢复时被吃掉）：`)`, `}`, `]`, `,`, `;`, `EOF`。

---

## 6. 扩展指南

### 6.1 添加新表达式类型

1. 在 `Ast.cs` 中添加新的 `record XxxExpr(...) : Expr(SourceSpan)`
2. 在 `ParseDeclarationOrStatement()` 或 `ParsePrimary()` 中添加匹配分支
3. 在 `Compiler.Visit()` 中添加分发分支

```csharp
// 示例：添加 while 循环
// 1. Ast.cs:
public record WhileExpr(Expr Cond, Expr Body, SourceSpan SourceSpan) : Expr(SourceSpan);

// 2. Parser.cs ParsePrimary():
if (Match(TokenType.While))
{
    Expr cond = ParseExpression();
    Expr body = Check(LeftBrace) ? ParseBlock() : ParseExpression();
    return new WhileExpr(cond, body, span);
}

// 3. Compiler.cs Visit():
case WhileExpr e: CompileWhile(e); break;
```

### 6.2 添加新运算符（修改优先级）

```csharp
// 示例：在 Factor 和 Unary 之间添加幂运算符 **（右结合）
private Expr ParsePower()
{
    Expr expr = ParseUnary();
    while (Match(TokenType.Power))  // 新 TokenType
    {
        Token op = Previous();
        Expr right = ParsePower();  // 递归到自身 = 右结合
        expr = new BinaryExpr(expr, op.Lexeme, right, span);
    }
    return expr;
}
```

### 6.3 添加新声明语法

修改 `ParseDeclarationOrStatement()`，在处理 `Match(TokenType.XXX)` 后调用新解析方法。

---

## 7. 解析过程示例

输入 `let add = (a, b) => a + b`：

```
Parse()
 └─ ParseDeclarationOrStatement()
     └─ Match(Let) → ParseLetDeclaration()
         ├─ Consume(Identifier) → "add"
         ├─ Consume(Equal) → "="
         └─ ParseExpression() → ParseAssignment() → ... → ParsePrimary()
             └─ Match(LeftParen)
                 └─ IsNextLambda() → true
                     └─ ParseLambda()
                         ├─ 参数: "a", "b"
                         ├─ Consume(Arrow) → "=>"
                         └─ body: ParseExpression()
                             └─ BinaryExpr("a", "+", "b")

AST:
LetExpr("add",
  LambdaExpr(["a","b"],
    BinaryExpr(IdentifierExpr("a"), "+", IdentifierExpr("b"))))
```

---

> **文档版本**: 1.0 | **对应源码**: `ScriptLang/Parser/` | **最后更新**: 2026-06-06
