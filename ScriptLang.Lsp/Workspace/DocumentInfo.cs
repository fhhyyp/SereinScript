using ScriptLang.Lexer;
using ScriptLang.Parser;
using ScriptLang.Lsp.Analysis;

namespace ScriptLang.Lsp.Workspace;

/// <summary>
/// 单个文档的解析信息
/// </summary>
public sealed class DocumentInfo
{
    /// <summary>文档 URI</summary>
    public Uri Uri { get; }

    /// <summary>文档文本内容</summary>
    public string Text { get; private set; }

    /// <summary>文档版本号（用于增量更新）</summary>
    public int Version { get; private set; }

    /// <summary>词法分析结果</summary>
    public List<Token> Tokens { get; private set; }

    /// <summary>语法分析结果</summary>
    public Expr? Ast { get; private set; }

    /// <summary>全局作用域（含所有嵌套作用域）</summary>
    public Scope? RootScope { get; private set; }

    public DocumentInfo(Uri uri, string text, int version)
    {
        Uri = uri;
        Text = text;
        Version = version;
        Tokens = [];
        Parse();
    }

    /// <summary>
    /// 更新文档内容并重新解析
    /// </summary>
    public void Update(string text, int version)
    {
        Text = text;
        Version = version;
        Parse();
    }

    private void Parse()
    {
        var filePath = Uri.LocalPath;

        // Lex
        var lexer = new Lexer.Lexer(Text, filePath);
        Tokens = lexer.ScanTokens();

        // Parse
        var parser = new Parser.Parser(Tokens, filePath);
        Ast = parser.Parse();

        // Build Symbol Table (pass directory for resolving relative script imports)
        var symTable = new SymbolTable();
        if (Ast is ProgramExpr program)
        {
            string? baseDir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            RootScope = symTable.Build(program, baseDir);
        }
    }

    public int GetOffset(int line, int character)
        => Utilities.PositionMapper.GetOffset(Text, line, character);

    public (int line, int character) GetPosition(int offset)
        => Utilities.PositionMapper.GetPosition(Text, offset);
}
