namespace ScriptLang.Lsp.Analysis;

/// <summary>
/// 符号类型枚举
/// </summary>
public enum ScriptSymbolKind
{
    /// <summary>let 声明（不可变变量）</summary>
    LetVariable,
    /// <summary>var 声明（可变变量）</summary>
    VarVariable,
    /// <summary>Lambda 参数</summary>
    Parameter,
    /// <summary>Lambda 表达式（函数）</summary>
    Function,
    /// <summary>import 导入的符号</summary>
    Import,
    /// <summary>内置函数（print, len 等）</summary>
    Builtin,
}

/// <summary>
/// 符号信息
/// </summary>
public sealed class SymbolInfo
{
    /// <summary>符号名称</summary>
    public string Name { get; }

    /// <summary>符号类型</summary>
    public ScriptSymbolKind Kind { get; }

    /// <summary>声明/定义所在的源码位置</summary>
    public Parser.SourceSpan SourceSpan { get; }

    /// <summary>所属作用域</summary>
    public Scope? ParentScope { get; internal set; }

    /// <summary>额外信息（如 import 路径）</summary>
    public string? Detail { get; init; }

    public SymbolInfo(string name, ScriptSymbolKind kind, Parser.SourceSpan sourceSpan)
    {
        Name = name;
        Kind = kind;
        SourceSpan = sourceSpan;
    }

    /// <summary>符号的 LSP SymbolKind 映射</summary>
    public OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind ToLspSymbolKind() => Kind switch
    {
        ScriptSymbolKind.LetVariable => OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind.Variable,
        ScriptSymbolKind.VarVariable => OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind.Variable,
        ScriptSymbolKind.Parameter => OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind.Variable,
        ScriptSymbolKind.Function => OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind.Function,
        ScriptSymbolKind.Import => OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind.Module,
        ScriptSymbolKind.Builtin => OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind.Function,
        _ => OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind.Variable,
    };

    public override string ToString() => $"{Kind}:{Name}";
}

/// <summary>
/// 作用域节点
/// </summary>
public sealed class Scope
{
    /// <summary>父作用域（null 表示全局作用域）</summary>
    public Scope? Parent { get; }

    /// <summary>子作用域列表</summary>
    public List<Scope> Children { get; } = [];

    /// <summary>此作用域内的符号（名称 → 符号信息）</summary>
    public Dictionary<string, SymbolInfo> Symbols { get; } = new(StringComparer.Ordinal);

    /// <summary>作用域对应的 AST 节点（用于编号）</summary>
    public Parser.Expr? Node { get; init; }

    public Scope(Scope? parent = null)
    {
        Parent = parent;
        parent?.Children.Add(this);
    }

    /// <summary>
    /// 在当前作用域链中按名称查找符号（由内向外）
    /// </summary>
    public SymbolInfo? Lookup(string name)
    {
        if (Symbols.TryGetValue(name, out var symbol))
            return symbol;

        return Parent?.Lookup(name);
    }

    /// <summary>
    /// 收集作用域链中所有可见符号
    /// </summary>
    public Dictionary<string, SymbolInfo> GetAllVisibleSymbols()
    {
        var result = new Dictionary<string, SymbolInfo>(StringComparer.Ordinal);

        // 从当前向根遍历（内层符号覆盖外层同名字段）
        var visited = new HashSet<Scope>();
        var stack = new Stack<Scope>();
        Scope? current = this;
        while (current != null)
        {
            foreach (var (name, symbol) in current.Symbols)
            {
                result.TryAdd(name, symbol);
            }
            current = current.Parent;
        }

        return result;
    }
}
