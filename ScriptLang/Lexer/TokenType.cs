namespace ScriptLang.Lexer;

/// <summary>
/// Token 类型枚举
/// </summary>
public enum TokenType
{
    // 单字符 Token
    /// <summary>token is `(`</summary>
    LeftParen,
    /// <summary>token is `)`</summary>
    RightParen,
    /// <summary>token is `{`</summary>
    LeftBrace,
    /// <summary>token is `}`</summary>
    RightBrace,
    /// <summary>token is `[`</summary>
    LeftBracket,
    /// <summary>token is `]`</summary>
    RightBracket,
    /// <summary>token is `,`</summary>
    Comma,
    /// <summary>token is `.`</summary>
    Dot,
    /// <summary>token is `?.`</summary>
    QuestionDot,
    /// <summary>token is `:`</summary>
    Colon,
    /// <summary>token is `;`</summary>
    Semicolon,

    // 运算符
    /// <summary>token is `+`</summary>
    Plus,
    /// <summary>token is `-`</summary>
    Minus,
    /// <summary>token is `*`</summary>
    Star,
    /// <summary>token is `/`</summary>
    Slash,
    /// <summary>token is `%`</summary>
    Percent,

    /// <summary>token is `=`</summary>
    Equal,              // =
    /// <summary>token is `==`</summary>
    EqualEqual,
    /// <summary>token is `!=`</summary>
    BangEqual,

    /// <summary>token is `&lt;`</summary>  
    Less,
    /// <summary>token is `&lt;=`</summary>  
    LessEqual,
    /// <summary>token is `&gt;`</summary>
    Greater,            // >
    /// <summary>token is `&gt;=`</summary>
    GreaterEqual,

    /// <summary>token is `!`</summary>
    Bang,
    /// <summary>token is `&&`</summary>
    And,
    /// <summary>token is `||`</summary>
    Or,

    /// <summary>token is `=>`</summary>
    Arrow,

    // 关键字
    /// <summary>token is `let`</summary>
    Let,
    /// <summary>token is `var`</summary>
    Var,
    /// <summary>token is `if`</summary>
    If,
    /// <summary>token is `then`</summary>
    Then,
    /// <summary>token is `else`</summary>
    Else,
    /// <summary>token is `when`</summary>
    When,
    /// <summary>token is `for`</summary>
    For,
    /// <summary>token is `in`</summary>
    In,
    /// <summary>token is `return`</summary>
    Return,

    // 模块导入
    /// <summary>token is `import`</summary>
    Import,
    /// <summary>token is `from`</summary>
    From,

    // 字面量
    /// <summary>token value is number `123` or `12.34` </summary>
    Number,
    /// <summary>token value is string `hello`</summary>
    String,
    /// <summary>token value is boolean `true` </summary>
    True,
    /// <summary>token value is boolean `false`</summary>
    False,
    /// <summary>token value is null</summary>
    Null,

    // 标识符
    /// <summary>token is an identifier, whose value is a variable name, member name, or method name.</summary>
    Identifier, 

    // 特殊
    /// <summary>end of file</summary>
    EOF,
    /// <summary>unknown token </summary>
    Unknown
}
