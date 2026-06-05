# SereinScript 词法解析器（Lexer）二次开发文档

> 本文档针对 `ScriptLang/Lexer/` 模块，面向需要扩展词法规则的二次开发者。
> 源码位置：`ScriptLang/Lexer/Lexer.cs` (766行), `Token.cs`, `TokenType.cs`

---

## 1. 架构概览

```
┌──────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Source     │ ──► │   Lexer.ScanTokens()  │ ──► │   List<Token>   │
│  (string)    │     │  逐字符扫描 + 状态机   │     │  + Diagnostics  │
└──────────────┘     └──────────────────┘     └─────────────────┘
```

Lexer 是一个**手写递归下降扫描器**，不使用正则表达式或生成器。它通过单次遍历源代码字符串，逐字符处理，生成 Token 列表。

### 核心类关系

```
TokenType (enum: 67种类型)
    ↓
Token (record: TokenIndex, Type, Lexeme, StartIndex, Length, Literal, FilePath, Line, Column)
    ↓
Lexer (class: 扫描主类)
    ↓
LexDiagnostic (record: 词法错误信息)
```

### 关键设计决策

| 决策 | 说明 |
|------|------|
| **手写扫描器** | 无外部依赖，性能可控，易于扩展 |
| **单文件扫描** | 每个 .script 文件对应一个 Lexer 实例 |
| **Token 不可变** | `Token` 是 `record`，创建后不可修改 |
| **错误恢复** | 遇到未知字符仍然生成 Unknown Token，不中断 |
| **行列追踪** | 每个 Token 携带精确的 `(Line, Column, StartIndex, Length)` |

---

## 2. Token 类型体系

### 2.1 分类总览

```csharp
public enum TokenType
{
    // ===== 单字符分隔符 (10个) =====
    LeftParen, RightParen,     // ( )
    LeftBrace, RightBrace,     // { }
    LeftBracket, RightBracket, // [ ]
    Comma, Dot,                // , .
    Colon, Semicolon,          // : ;
    QuestionDot,               // ?.

    // ===== 运算符 (17个) =====
    Plus, Minus, Star, Slash, Percent,  // + - * / %
    Equal, EqualEqual, BangEqual,       // = == !=
    Less, LessEqual, Greater, GreaterEqual, // < <= > >=
    Bang, And, Or,                      // ! && ||
    Arrow,                              // => (或 -> )

    // ===== 关键字 (11个) =====
    Let, Var, If, Then, Else,
    When, For, In, Return,
    Import, From,

    // ===== 数值字面量 (5个) =====
    Number_Int, Number_Long, Number_Float,
    Number_Double, Number_Decimal,

    // ===== 其他字面量 (4个) =====
    String, True, False, Null,

    // ===== 元类型 (3个) =====
    Identifier, EOF, Unknown
}
```

### 2.2 Token 数据结构

```csharp
public record Token(
    int TokenIndex,      // Token 在列表中的序号 (0-based)
    TokenType Type,      // Token 类型
    string Lexeme,       // 源代码中的原始文本
    int StartIndex,      // 在全文字符偏移
    int Length,          // 词素长度
    object? Literal,     // 解析后的字面量值 (int/long/float/...)
    string? FilePath,    // 源文件路径
    int Line,            // 行号 (1-based)
    int Column           // 列号 (1-based)
)
```

---

## 3. 核心扫描逻辑

### 3.1 主循环

```csharp
public List<Token> ScanTokens()
{
    while (!IsAtEnd())           // _position >= _source.Length
    {
        ScanToken();
    }
    _tokens.Add(EOF);            // 始终以 EOF 结尾
    return _tokens;
}
```

### 3.2 ScanToken() 分发

`ScanToken()` 使用巨大的 `switch(c)` 按首字符分发到各处理分支：

```csharp
case '(': AddToken(LeftParen);   // 直接单字符
case '!':  Match('=') ? BangEqual : Bang;  // 前瞻一位
case '=':  Match('=') ? EqualEqual : Match('>') ? Arrow : Equal;
case '/':  Match('/') ? SkipComment() : AddToken(Slash);  // 注释处理
case '"':  ReadString();          // 字符串状态机
case '0'-'9':  ReadNumber();     // 数值状态机
case 'a'-'z'|'A'-'Z'|'_'|'@': ReadIdentifier();  // 标识符/关键字
```

### 3.3 辅助方法

| 方法 | 功能 |
|------|------|
| `Advance()` | `_column++; return _source[_position++]` |
| `Peek()` | 查看当前字符，不前进 |
| `PeekNext()` | 查看下下个字符 (`_source[_position+1]`) |
| `Match(char)` | 如果当前字符匹配则前进并返回 `true` |
| `IsAtEnd()` | `_position >= _source.Length` |

---

## 4. 各词法单元处理详解

### 4.1 注释

仅支持 `//` 单行注释。遇到 `//` 后，持续 `Advance()` 直到 `\n` 或文件末尾，**不生成任何 Token**。

```csharp
case '/':
    if (Match('/'))
    {
        while (Peek() != '\n' && !IsAtEnd()) Advance();
    }
    else AddToken(TokenType.Slash, "/");
```

> **扩展点**: 如需支持 `/* ... */`，可在此处添加 Match('*') 后的块注释扫描。

### 4.2 字符串

```csharp
private void ReadString()
{
    while (!IsAtEnd() && Peek() != '"')
    {
        if (Peek() == '\n') break;           // 不允许跨行
        if (Peek() == '\\') { Advance(); sb.Append(Advance()); }  // 转义
        else sb.Append(Advance());
    }
    bool closed = Peek() == '"'; if (closed) Advance();
    AddToken(TokenType.String, sb.ToString(), sb.ToString());
    if (!closed) Diagnostics.Add("未终止的字符串");
}
```

关键行为：
- 不支持跨行（遇到 `\n` 断开）
- 转义处理简单：`\\` + 下一个字符作为原义字符保留
- 未闭合的字符串会生成 Diagnostic 但不中断扫描

> **已废弃的 v1 版本**: `ReadString_v1()` 包含完整转义映射 (`\n` → 换行, `\t` → 制表符 等)，可作为升级参考。

### 4.3 数值字面量

这是最复杂的扫描逻辑。完整流程：

```
ReadNumber()
├── 0x / 0X → ReadHexDigits() → Parse with suffix
├── 0b / 0B → ReadBinaryDigits() → Parse with suffix
└── 十进制
    ├── 整数部分
    ├── 可选 .小数部分
    └── 可选后缀 (u/l/ul/f/d/m)
```

#### 后缀系统

```csharp
private string? ReadNumberSuffix()
{
    // 优先级: u → ul; l; f; d; m
    if (tolower(Peek()) == 'u') { ... }  // u 或 ul (已注释)
    if (tolower(Peek()) == 'l') { ... }  // L/l
    if (tolower(Peek()) == 'f') { ... }  // F/f
    if (tolower(Peek()) == 'd') { ... }  // D/d
    if (tolower(Peek()) == 'm') { ... }  // M/m
}
```

#### 类型推断

无后缀时的自动推断策略：

| 值特征 | 推断类型 | 说明 |
|--------|----------|------|
| 整数，在 `int` 范围内 | `Number_Int` | 优先 int |
| 整数，超出 `int`，在 `long` 范围内 | `Number_Long` | 升级为 long |
| 整数，超出 `long` | `Number_Double` | 回退到 double |
| 含小数点 | `Number_Double` | 默认 double |

#### 进制解析

```csharp
// 十六进制: Convert.ToInt64(hexPart, 16)
// 二进制: Convert.ToInt64(binPart, 2)
// 解析后按后缀或范围确定最终类型
```

> **扩展点**: `Number_UInt` 和 `Number_ULong` 已在枚举与解析逻辑中注释掉，如需启用无符号类型，取消相关注释即可。

### 4.4 标识符与关键字

```csharp
private void ReadIdentifier()
{
    // 回退一位（第一个字符已在 ScanToken 中被 Advance 读取）
    _position--; _column--;
    while (char.IsLetterOrDigit(Peek()) || Peek() == '_' || Peek() == '@')
        sb.Append(Advance());

    string lexeme = sb.ToString();
    TokenType type = lexeme switch
    {
        "let" => Let, "var" => Var,
        "if" => If, "then" => Then, "else" => Else,
        "when" => When, "for" => For, "in" => In,
        "return" => Return, "import" => Import, "from" => From,
        "true" => True, "false" => False, "null" => Null,
        _ => Identifier
    };
    // bool/null 的 literal 在此处填充
}
```

**设计要点**:
- `@` 前缀的标识符用于与 C# 关键字冲突的场景
- 关键字检测使用 `switch` 表达式，没有 O(n) 字典查找开销
- `print` 被显式标记为 `Identifier`（作为内置函数而不是关键字）

---

## 5. 错误处理与诊断

### 5.1 当前错误类型

| 场景 | 处理方式 |
|------|----------|
| 未知单字符 `&` (单独) | 生成 `Unknown` Token |
| 未知单字符 `\|` (单独) | 生成 `Unknown` Token |
| 未知单字符 `?` (单独) | 生成 `Unknown` Token |
| 未终止的字符串 | 添加 `LexDiagnostic`，继续扫描 |
| 其他意外字符 | 添加 `LexDiagnostic`，生成 `Unknown` Token |

### 5.2 LexDiagnostic 结构

```csharp
public record LexDiagnostic(
    string Message,   // 错误描述
    string FilePath,  // 文件路径
    int Line,         // 行号
    int Column,       // 列号
    int Length        // 错误跨度
);
```

### 5.3 改进建议

1. **增加错误累积**: 当前 Lexer 生成 Unknown Token 后继续扫描，但调用方（`ScriptEngine`）只检查 Parser 阶段的异常，Lexer 的 `Diagnostics` 未被主动收集。建议在 `CreateTask` 中增加 `lexer.Diagnostics` 的检查。
2. **增加字符串转义**: 当前 `ReadString()` 仅保留原始转义序列，建议引入 `ReadString_v1()` 中的转义映射表。
3. **增加插值字符串**: 如 `$"Hello {name}"`。

---

## 6. 扩展指南

### 6.1 添加新关键字

1. 在 `TokenType` 枚举中添加新成员
2. 在 `ReadIdentifier()` 的 `switch` 中添加映射
3. 在 Parser 中添加对应的语法规则

```csharp
// 示例：添加 "while" 关键字
// TokenType.cs:
While,   // token 是 `while`

// Lexer.cs ReadIdentifier():
"while" => TokenType.While,
```

### 6.2 添加新运算符

1. 在 `TokenType` 中添加新成员
2. 在 `ScanToken()` 的 `switch` 中添加匹配逻辑

```csharp
// 示例：添加 "**" 幂运算符
// ScanToken():
case '*':
    if (Match('*'))
        AddToken(TokenType.Power, "**");
    else
        AddToken(TokenType.Star, "*");
    break;
```

### 6.3 添加新字面量类型

```csharp
// 示例：添加单引号字符字面量 'a'
// ScanToken():
case '\'': ReadChar(); break;

// 新方法:
private void ReadChar()
{
    char c = Advance();
    if (Peek() == '\'') Advance(); // 消费结束引号
    else Diagnostics.Add(...);
    AddToken(TokenType.CharLiteral, c.ToString(), c);
}
```

### 6.4 添加注释类型

```csharp
// 示例：块注释
case '/':
    if (Match('/'))
        SingleLineComment();
    else if (Match('*'))
        BlockComment();  // 需要新增方法
    else
        AddToken(TokenType.Slash, "/");
```

---

## 7. 性能特征

| 操作 | 复杂度 | 说明 |
|------|--------|------|
| 单字符 Token 扫描 | O(1) | 直接 `AddToken` |
| 标识符/关键字识别 | O(n) | n = 标识符长度，switch 表达式 O(1) |
| 字符串扫描 | O(n) | n = 字符串长度 |
| 数值扫描 | O(n) | n = 数值文本长度 |
| 整体扫描 | O(N) | N = 源代码总长度，单次遍历 |

### 已知优化

- 小整数缓存：数值 `-1`, `0`, `1` 在 VM 层有专用 `Load*` 指令
- `-128 ~ 127` 范围的 int 在 `NumberValueCache` 中有全局缓存

---

## 8. 测试用例

### 8.1 基础用例

```js
let x = 42         // Identifier, Identifier, Equal, Number_Int
"hello\n"          // String (含转义)
0xFF               // Number_Int (hex)
0b1010             // Number_Int (bin)
3.14f != 3.14d     // Number_Float, BangEqual, Number_Double
```

### 8.2 边界用例

```js
""                // 空字符串
0                 // 最小整数
99999999999999999 // 超大整数 → double
0x                // 十六进制前缀后无数字 (当前会生成空数字 Token)
```

### 8.3 错误用例

```js
"unclosed        // 诊断：未终止的字符串
@#$              // 多个 Unknown Token
& | ?            // 单独使用，生成 Unknown
```

---

> **文档版本**: 1.0 | **对应源码**: `ScriptLang/Lexer/` | **最后更新**: 2026-06-06
