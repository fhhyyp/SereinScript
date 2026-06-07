using ScriptLang.Parser;

namespace ScriptLang.Lsp.Analysis;

/// <summary>
/// 符号表 — 管理文档符号、查找引用、全局可见符号
/// </summary>
public sealed class SymbolTable
{
    /// <summary>内置函数名</summary>
    private static readonly HashSet<string> BuiltinNames = new(StringComparer.Ordinal)
    {
        "print", "debug", "now", "sleep", "typeof", "range", "len", "keys",
        "bool", "int", "double", "str", "try",
        // system modules
        "file", "network", "console", "path", "json", "timer", "crypto", "process",
    };

    /// <summary>
    /// 从 AST 构建文档的完整符号表
    /// </summary>
    public Scope Build(ProgramExpr ast, string? basePath = null)
    {
        return ScopeResolver.BuildScopes(ast, BuiltinNames, basePath);
    }

    /// <summary>
    /// 获取所有可见符号（给定位置的作用域链）
    /// </summary>
    public Dictionary<string, SymbolInfo> GetVisibleSymbols(Scope rootScope, int offset)
    {
        var scope = ScopeResolver.FindScopeAt(rootScope, offset);
        if (scope == null) return new Dictionary<string, SymbolInfo>(StringComparer.Ordinal);

        return scope.GetAllVisibleSymbols();
    }

    /// <summary>
    /// 在全局作用域树中查找指定名称的所有引用
    /// </summary>
    /// <param name="rootScope">全局作用域</param>
    /// <param name="name">符号名称</param>
    /// <param name="ast">AST（用于查找 IdentifierExpr 引用）</param>
    /// <returns>所有引用的 SourceSpan 列表</returns>
    public List<SourceSpan> FindReferences(Scope rootScope, string name, Expr ast)
    {
        var references = new List<SourceSpan>();
        FindIdentifierReferences(ast, name, references);
        return references;
    }

    /// <summary>
    /// 获取特定符号的 SymbolInfo（通过位置查找）
    /// </summary>
    public SymbolInfo? GetSymbolAt(Scope rootScope, int offset, Expr ast)
    {
        // 先找 AST 节点
        var node = Utilities.PositionMapper.FindNodeAt(ast, offset);
        if (node is IdentifierExpr id)
        {
            var scope = ScopeResolver.FindScopeAt(rootScope, offset);
            return scope?.Lookup(id.Name);
        }
        else if (node is LetExpr let)
        {
            var scope = ScopeResolver.FindScopeAt(rootScope, offset);
            return scope?.Lookup(let.Name);
        }
        else if (node is VarExpr var)
        {
            var scope = ScopeResolver.FindScopeAt(rootScope, offset);
            return scope?.Lookup(var.Name);
        }

        return null;
    }

    /// <summary>
    /// 查找文档中所有的 IdentifierExpr（对指定名称的引用）
    /// </summary>
    private static void FindIdentifierReferences(Expr? expr, string targetName, List<SourceSpan> results)
    {
        if (expr == null) return;

        switch (expr)
        {
            case IdentifierExpr id when id.Name == targetName:
                results.Add(id.SourceSpan);
                break;

            case ProgramExpr e: foreach (var s in e.Statements) FindIdentifierReferences(s, targetName, results); break;
            case BlockExpr e: foreach (var s in e.Statements) FindIdentifierReferences(s, targetName, results); break;
            case LetExpr e: FindIdentifierReferences(e.Value, targetName, results); break;
            case VarExpr e: FindIdentifierReferences(e.Value, targetName, results); break;
            case AssignExpr e: FindIdentifierReferences(e.Value, targetName, results); break;
            case BinaryExpr e: FindIdentifierReferences(e.Left, targetName, results); FindIdentifierReferences(e.Right, targetName, results); break;
            case UnaryExpr e: FindIdentifierReferences(e.Expr, targetName, results); break;
            case ConditionalExpr e: FindIdentifierReferences(e.Cond, targetName, results); FindIdentifierReferences(e.Then, targetName, results); FindIdentifierReferences(e.Else, targetName, results); break;
            case IfExpr e: FindIdentifierReferences(e.Cond, targetName, results); FindIdentifierReferences(e.Then, targetName, results); FindIdentifierReferences(e.Else, targetName, results); break;
            case WhenExpr e:
                FindIdentifierReferences(e.Value, targetName, results);
                foreach (var c in e.Clauses) { FindIdentifierReferences(c.Pattern, targetName, results); FindIdentifierReferences(c.Body, targetName, results); }
                if (e.OtherClause != null) FindIdentifierReferences(e.OtherClause.Body, targetName, results);
                break;
            case ForExpr e: FindIdentifierReferences(e.Iterable, targetName, results); FindIdentifierReferences(e.Body, targetName, results); break;
            case LambdaExpr e: FindIdentifierReferences(e.Body, targetName, results); break;
            case CallExpr e: FindIdentifierReferences(e.Target, targetName, results); foreach (var a in e.Args) FindIdentifierReferences(a, targetName, results); break;
            case ReturnExpr e: if (e.Value != null) FindIdentifierReferences(e.Value, targetName, results); break;
            case ArrayLiteralExpr e: foreach (var el in e.Elements) FindIdentifierReferences(el, targetName, results); break;
            case ObjectLiteralExpr e: foreach (var p in e.Properties) FindIdentifierReferences(p.Value, targetName, results); break;
            case MemberAccessExpr e: FindIdentifierReferences(e.Target, targetName, results); break;
            case MemberAssignExpr e: FindIdentifierReferences(e.Target, targetName, results); FindIdentifierReferences(e.Value, targetName, results); break;
            case IndexAccessExpr e: FindIdentifierReferences(e.Target, targetName, results); FindIdentifierReferences(e.Index, targetName, results); break;
            case IndexAssignExpr e: FindIdentifierReferences(e.Target, targetName, results); FindIdentifierReferences(e.Index, targetName, results); FindIdentifierReferences(e.Value, targetName, results); break;
        }
    }
}
