using System.Text;

namespace ScriptLang.Lexer;



/// <summary>
/// 词法分析器
/// </summary>
public class Lexer
{
    private readonly string _source;
    private readonly List<Token> _tokens = new();
    private int _position = 0;
    private int _line = 1;
    private int _column = 1;

    //private readonly string filePath;
    public string? FilePath { get; set; } = string.Empty;
    
   
    public Lexer(string source, string filePath)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        FilePath = filePath;
    }
    
    /// <summary>
    /// 执行词法分析
    /// </summary>
    public List<Token> ScanTokens()
    {
        while (!IsAtEnd())
        {
            ScanToken();
        }
        
        _tokens.Add(new Token(_tokens.Count, TokenType.EOF, "",  _position, 0, null, FilePath, _line, _column));
        return _tokens;
    }
    
    private void ScanToken()
    {
        char c = Advance();
        
        // DEBUG: 打印当前字符和位置
        // Console.WriteLine($"DEBUG: char='{c}' pos={_position} line={_line} col={_column}");
        
        switch (c)
        {
            // 跳过空白字符
            case ' ':
            case '\r':
            case '\t':
                break;
            case '\n':
                _line++;
                _column = 0; // Will be incremented to 1 on next Advance
                break;
                
            // 单字符 Token
            case '(': AddToken(TokenType.LeftParen, "("); break;
            case ')': AddToken(TokenType.RightParen, ")"); break;
            case '{': AddToken(TokenType.LeftBrace, "{"); break;
            case '}': AddToken(TokenType.RightBrace, "}"); break;
            case '[': AddToken(TokenType.LeftBracket, "["); break;
            case ']': AddToken(TokenType.RightBracket, "]"); break;
            case ',': AddToken(TokenType.Comma, ","); break;
            case '.': AddToken(TokenType.Dot, "."); break;
            case ':': AddToken(TokenType.Colon, ":"); break;
            case ';': AddToken(TokenType.Semicolon, ";"); break;
            case '+': AddToken(TokenType.Plus, "+"); break;
            case '*': AddToken(TokenType.Star, "*"); break;
            case '%': AddToken(TokenType.Percent, "%"); break;
            case '!':
                if (Match('='))
                    AddToken(TokenType.BangEqual, "!=");
                else
                    AddToken(TokenType.Bang, "!");
                break;
            case '=':
                if (Match('='))
                    AddToken(TokenType.EqualEqual, "==");
                else if (Match('>'))
                    AddToken(TokenType.Arrow, "=>");
                else
                    AddToken(TokenType.Equal, "=");
                break;
            case '<':
                if (Match('='))
                    AddToken(TokenType.LessEqual, "<=");
                else
                    AddToken(TokenType.Less, "<");
                break;
            case '>':
                if (Match('='))
                    AddToken(TokenType.GreaterEqual, ">=");
                else
                    AddToken(TokenType.Greater, ">");
                break;
            case '-':
                if (Match('>'))
                    AddToken(TokenType.Arrow, "->");
                else
                    AddToken(TokenType.Minus, "-");
                break;
            case '/':
                // 注释支持
                if (Match('/'))
                {
                    while (Peek() != '\n' && !IsAtEnd()) Advance();
                }
                else
                {
                    AddToken(TokenType.Slash, "/");
                }
                break;
            case '&':
                if (Match('&'))
                    AddToken(TokenType.And, "&&");
                else
                    AddToken(TokenType.Unknown, "&");
                break;
            case '|':
                if (Match('|'))
                    AddToken(TokenType.Or, "||");
                else
                    AddToken(TokenType.Unknown, "|");
                break;
            case '?':
                if (Match('.'))
                    AddToken(TokenType.QuestionDot, "?.");
                else
                    AddToken(TokenType.Unknown, "?");
                break;
                
            // 字符串字面量
            case '"': ReadString(); break;
                
            // 字面量
            default:
                if (char.IsDigit(c))
                    ReadNumber(); // 解析数值
                else if (char.IsLetter(c) || c == '_' || c == '@')
                    ReadIdentifier();
                else
                    AddToken(TokenType.Unknown, c.ToString());
                break;
        }
    }
    
    private void ReadString()
    {
        StringBuilder sb = new();
        
        while (Peek() != '"' && !IsAtEnd())
        {
            if (Peek() == '\\' && PeekNext() != '\0')
            {
                Advance(); // 跳过反斜杠
                char next = Advance();
                sb.Append(next switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => next
                });
            }
            else
            {
                sb.Append(Advance());
            }
        }
        
        if (IsAtEnd())
            throw new Exception($"Unterminated string at line {_line}, column {_column}");
            
        Advance(); // 跳过结束引号
        
        AddToken(TokenType.String, sb.ToString(), sb.ToString());
    }
    
    private void ReadNumber()
    {
        StringBuilder sb = new();
        
        // 第一个数字已经在 ScanToken 中被读取，需要回退
        _position--;
        _column--;
        if (_column < 1) _column = 1;
        
        while (char.IsDigit(Peek()))
        {
            sb.Append(Advance());
        }
        
        // 处理小数部分
        if (Peek() == '.' && char.IsDigit(PeekNext()))
        {
            sb.Append(Advance()); // 小数点
            while (char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }
        
        string numberStr = sb.ToString();
        if (double.TryParse(numberStr, System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.InvariantCulture, out double number))
        {
            AddToken(TokenType.Number, numberStr, number);
        }
        else
        {
            AddToken(TokenType.Unknown, numberStr);
        }
    }
    
    private void ReadIdentifier()
    {
        StringBuilder sb = new();
        
        // 第一个字符已经在 ScanToken 中被读取，需要从 _source 中获取
        _position--; // 回退位置
        _column--;    // 回退列
        if (_column < 1) _column = 1;
        
        while (char.IsLetterOrDigit(Peek()) || Peek() == '_'|| Peek() == '@')
        {
            sb.Append(Advance());
        }
        
        string lexeme = sb.ToString();
        TokenType type = lexeme switch
        {
            "let" => TokenType.Let,
            "var" => TokenType.Var,
            "if" => TokenType.If,
            "then" => TokenType.Then,
            "else" => TokenType.Else,
            "when" => TokenType.When,
            "for" => TokenType.For,
            "in" => TokenType.In,
            "return" => TokenType.Return,
            "from" => TokenType.From,
            "import" => TokenType.Import,
            "true" => TokenType.True,
            "false" => TokenType.False,
            "null" => TokenType.Null,
            "print" => TokenType.Identifier, // 内置函数，作为标识符
            _ => TokenType.Identifier
        };
        
        object? literal = type switch
        {
            TokenType.True => true,
            TokenType.False => false,
            TokenType.Null => null,
            _ => null
        };
        
        AddToken(type, lexeme, literal);
    }
    
    private void AddToken(TokenType type, string lexeme, object? literal = null)
    {
        int startColumn = _column - lexeme.Length;
        _tokens.Add(new Token(_tokens.Count, type, lexeme, _position, lexeme.Length, literal, FilePath, _line, startColumn < 1 ? 1 : startColumn));
    }
    
    private char Advance()
    {
        _column++;
        return _source[_position++];
    }
    
    private bool IsAtEnd() => _position >= _source.Length;
    
    private char Peek()
    {
        if (IsAtEnd()) return '\0';
        return _source[_position];
    }
    
    private char PeekNext()
    {
        if (_position + 1 >= _source.Length) return '\0';
        return _source[_position + 1];
    }
    
    private bool Match(char expected)
    {
        if (IsAtEnd() || _source[_position] != expected) return false;
        _position++;
        _column++;
        return true;
    }
}
