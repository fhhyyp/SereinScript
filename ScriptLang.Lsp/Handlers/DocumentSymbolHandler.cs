using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ScriptLang.Lsp.Analysis;
using ScriptLang.Lsp.Workspace;
using ScriptLang.Parser;
using LspModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace ScriptLang.Lsp.Handlers;

/// <summary>
/// 文档符号处理器 — 提供文档大纲（变量声明、函数、import 等）
/// </summary>
public sealed class DocumentSymbolHandler : IDocumentSymbolHandler
{
    private readonly WorkspaceManager _workspace;

    public DocumentSymbolHandler(WorkspaceManager workspace)
    {
        _workspace = workspace;
    }

    public DocumentSymbolRegistrationOptions GetRegistrationOptions(
        DocumentSymbolCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DocumentSymbolRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter { Pattern = "**/*.script" })
        };
    }

    public Task<SymbolInformationOrDocumentSymbolContainer?> Handle(
        DocumentSymbolParams request, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine($"[LSP.Sym] uri={request.TextDocument.Uri}");
        var doc = _workspace.GetDocument(request.TextDocument.Uri);
        if (doc?.Ast is not ProgramExpr program)
            return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(null);

        var symbols = new List<SymbolInformationOrDocumentSymbol>();
        CollectSymbols(program, doc.Text, symbols);

        return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(
            new SymbolInformationOrDocumentSymbolContainer(symbols));
    }

    // ==================== 符号收集 ====================

    private static void CollectSymbols(Expr expr, string text,
        List<SymbolInformationOrDocumentSymbol> result)
    {
        switch (expr)
        {
            case ProgramExpr program:
                foreach (var stmt in program.Statements)
                    CollectSymbols(stmt, text, result);
                break;

            case BlockExpr block:
                foreach (var stmt in block.Statements)
                    CollectSymbols(stmt, text, result);
                break;

            case LetExpr let:
                AddSymbol(let.Name, ScriptSymbolKind.LetVariable, let.SourceSpan, text, result,
                    children: CollectChildren(let.Value, text));
                // 递归收集 lambda 体内的符号
                CollectBodySymbols(let.Value, text, result);
                break;

            case VarExpr var:
                AddSymbol(var.Name, ScriptSymbolKind.VarVariable, var.SourceSpan, text, result,
                    children: CollectChildren(var.Value, text));
                CollectBodySymbols(var.Value, text, result);
                break;

            case ImportStmt import:
                foreach (var (member, alias) in import.Members)
                {
                    var name = alias ?? member;
                    AddSymbol(name, ScriptSymbolKind.Import, import.SourceSpan, text, result,
                        detail: $"from \"{import.FilePath}\"");
                }
                break;

            case LambdaExpr lambda:
                // Lambda 本身作为函数符号
                AddSymbol("(lambda)", ScriptSymbolKind.Function, lambda.SourceSpan, text, result,
                    children: GetLambdaChildren(lambda, text));
                // 递归收集 lambda 体内的符号
                CollectBodySymbols(lambda.Body, text, result);
                break;

            case ForExpr forExpr:
                AddSymbol(forExpr.VarName, ScriptSymbolKind.VarVariable, forExpr.SourceSpan, text, result);
                CollectBodySymbols(forExpr.Body, text, result);
                break;

            // 递归处理容器类型
            case IfExpr ifExpr:
                CollectBodySymbols(ifExpr.Then, text, result);
                CollectBodySymbols(ifExpr.Else, text, result);
                break;

            case WhenExpr whenExpr:
                foreach (var clause in whenExpr.Clauses)
                    CollectBodySymbols(clause.Body, text, result);
                if (whenExpr.OtherClause != null)
                    CollectBodySymbols(whenExpr.OtherClause.Body, text, result);
                break;

            case AssignExpr assign:
                AddSymbol(assign.Name, ScriptSymbolKind.VarVariable, assign.SourceSpan, text, result);
                break;
        }
    }

    /// <summary>递归收集表达式内部的符号</summary>
    private static void CollectBodySymbols(Expr? expr, string text, List<SymbolInformationOrDocumentSymbol> result)
    {
        if (expr == null) return;

        switch (expr)
        {
            case BlockExpr block:
            case ProgramExpr program:
                // 这两者已经在 CollectSymbols 中被处理
                CollectSymbols(expr, text, result);
                break;
            case LambdaExpr lambda:
                CollectSymbols(lambda, text, result);
                break;
            case ForExpr forExpr:
                CollectSymbols(forExpr, text, result);
                break;
            case LetExpr let:
            case VarExpr var:
            case ImportStmt:
                CollectSymbols(expr, text, result);
                break;
            // 其他类型不再深入（避免重复收集）
        }
    }

    /// <summary>收集符号的子节点</summary>
    private static List<SymbolInformationOrDocumentSymbol>? CollectChildren(Expr? expr, string text)
    {
        if (expr is not LambdaExpr lambda) return null;

        var children = new List<SymbolInformationOrDocumentSymbol>();

        // Lambda 参数作为子节点
        foreach (var param in lambda.Params)
        {
            AddSimpleSymbol(param, ScriptSymbolKind.Parameter, lambda.SourceSpan, text, children);
        }

        // 递归收集 Lambda 体内的声明作为子节点
        if (lambda.Body is BlockExpr block)
        {
            foreach (var stmt in block.Statements)
                CollectSymbols(stmt, text, children);
        }

        return children.Count > 0 ? children : null;
    }

    /// <summary>获取 Lambda 的子节点（参数）</summary>
    private static List<SymbolInformationOrDocumentSymbol>? GetLambdaChildren(LambdaExpr lambda, string text)
    {
        var children = new List<SymbolInformationOrDocumentSymbol>();
        foreach (var param in lambda.Params)
        {
            AddSimpleSymbol(param, ScriptSymbolKind.Parameter, lambda.SourceSpan, text, children);
        }
        return children.Count > 0 ? children : null;
    }

    /// <summary>创建带层级的文档符号</summary>
    private static void AddSymbol(string name, ScriptSymbolKind kind, SourceSpan span, string text,
        List<SymbolInformationOrDocumentSymbol> result,
        List<SymbolInformationOrDocumentSymbol>? children = null,
        string? detail = null)
    {
        var start = Utilities.PositionMapper.GetPosition(text, span.Start);
        var end = Utilities.PositionMapper.GetPosition(text, span.Start + span.Length);

        var lspKind = kind switch
        {
            ScriptSymbolKind.LetVariable => LspModels.SymbolKind.Constant,
            ScriptSymbolKind.VarVariable => LspModels.SymbolKind.Variable,
            ScriptSymbolKind.Parameter => LspModels.SymbolKind.Variable,
            ScriptSymbolKind.Function => LspModels.SymbolKind.Function,
            ScriptSymbolKind.Import => LspModels.SymbolKind.Module,
            ScriptSymbolKind.Builtin => LspModels.SymbolKind.Function,
            _ => OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind.Variable
        };

        var symbol = new DocumentSymbol
        {
            Name = name,
            Kind = lspKind,
            Detail = detail,
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                start.line, start.character, end.line, end.character),
            SelectionRange = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                start.line, start.character, end.line, end.character),
            Children = children != null ? new Container<DocumentSymbol>(children.Select(c =>
                c.DocumentSymbol!).ToList()) : null
        };

        result.Add(new SymbolInformationOrDocumentSymbol(symbol));
    }

    /// <summary>创建简单的无子节点符号</summary>
    private static void AddSimpleSymbol(string name, ScriptSymbolKind kind, SourceSpan span, string text,
        List<SymbolInformationOrDocumentSymbol> result)
    {
        AddSymbol(name, kind, span, text, result);
    }
}
