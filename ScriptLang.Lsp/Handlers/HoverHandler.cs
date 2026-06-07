using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ScriptLang.Lsp.Analysis;
using ScriptLang.Lsp.Utilities;
using ScriptLang.Lsp.Workspace;
using ScriptLang.Parser;

namespace ScriptLang.Lsp.Handlers;

/// <summary>
/// 悬停信息处理器 — 显示光标处符号的类型和文档
/// </summary>
public sealed class HoverHandler : IHoverHandler
{
    private readonly WorkspaceManager _workspace;
    private readonly SymbolTable _symbolTable;

    public HoverHandler(WorkspaceManager workspace, SymbolTable symbolTable)
    {
        _workspace = workspace;
        _symbolTable = symbolTable;
    }

    public HoverRegistrationOptions GetRegistrationOptions(
        HoverCapability capability, ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter { Pattern = "**/*.script" })
        };
    }

    public Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        var doc = _workspace.GetDocument(request.TextDocument.Uri);
        if (doc?.Ast == null || doc.RootScope == null)
            return Task.FromResult<Hover?>(null);

        int offset = doc.GetOffset((int)request.Position.Line, (int)request.Position.Character);

        // 查找光标处的 AST 节点
        var node = PositionMapper.FindNodeAt(doc.Ast, offset);
        if (node == null)
            return Task.FromResult<Hover?>(null);

        // 查找当前作用域
        var scope = ScopeResolver.FindScopeAt(doc.RootScope, offset);

        MarkedStringsOrMarkupContent? content = node switch
        {
            IdentifierExpr id => BuildIdentifierHover(id, scope),
            LetExpr let => BuildDeclarationHover(let.Name, "let", scope, let.SourceSpan),
            VarExpr var => BuildDeclarationHover(var.Name, "var", scope, var.SourceSpan),
            LiteralExpr lit => BuildLiteralHover(lit),
            LambdaExpr => new MarkedStringsOrMarkupContent(
                new MarkupContent { Kind = MarkupKind.Markdown, Value = "```sereinscript\n(params) => body\n```\nLambda 表达式" }),
            BinaryExpr bin => new MarkedStringsOrMarkupContent(
                new MarkupContent { Kind = MarkupKind.Markdown, Value = $"操作符 `{bin.Op}`" }),
            _ => null
        };

        if (content == null)
            return Task.FromResult<Hover?>(null);

        var range = SourceSpanToRange(node.SourceSpan, doc.Text);
        return Task.FromResult<Hover?>(new Hover
        {
            Contents = content,
            Range = range
        });
    }

    // ==================== 悬停信息生成 ====================

    private static MarkedStringsOrMarkupContent? BuildIdentifierHover(IdentifierExpr id, Scope? scope)
    {
        var symbol = scope?.Lookup(id.Name);
        if (symbol == null) return null;

        string kindLabel = symbol.Kind switch
        {
            ScriptSymbolKind.LetVariable => "let（不可变）",
            ScriptSymbolKind.VarVariable => "var（可变）",
            ScriptSymbolKind.Parameter => "参数",
            ScriptSymbolKind.Function => "函数",
            ScriptSymbolKind.Builtin => "内置函数",
            ScriptSymbolKind.Import => "导入",
            _ => "变量"
        };

        var md = new global::System.Text.StringBuilder();
        md.AppendLine($"```sereinscript");
        md.AppendLine($"{kindLabel}: {symbol.Name}");
        if (symbol.Detail != null)
            md.AppendLine(symbol.Detail);
        md.AppendLine("```");

        return new MarkedStringsOrMarkupContent(
            new MarkupContent { Kind = MarkupKind.Markdown, Value = md.ToString() });
    }

    private static MarkedStringsOrMarkupContent? BuildDeclarationHover(string name, string kind, Scope? scope, SourceSpan span)
    {
        var symbol = scope?.Lookup(name);
        if (symbol == null) return null;

        var md = new global::System.Text.StringBuilder();
        md.AppendLine($"```sereinscript");
        md.AppendLine($"{kind} {name}");
        md.AppendLine("```");
        md.AppendLine($"声明位置: L{span.StartLine + 1}:C{span.StartColumn + 1}");

        return new MarkedStringsOrMarkupContent(
            new MarkupContent { Kind = MarkupKind.Markdown, Value = md.ToString() });
    }

    private static MarkedStringsOrMarkupContent? BuildLiteralHover(LiteralExpr lit)
    {
        string type = lit.Value switch
        {
            null => "null",
            bool => "bool",
            int => "int",
            long => "long",
            float => "float",
            double => "double",
            decimal => "decimal",
            string => "string",
            _ => lit.Value.GetType().Name
        };

        return new MarkedStringsOrMarkupContent(
            new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = $"```sereinscript\n{lit.Value}\n```\n类型: `{type}`"
            });
    }

    // ==================== 辅助 ====================

    private static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range SourceSpanToRange(SourceSpan span, string text)
    {
        var start = PositionMapper.GetPosition(text, span.Start);
        var end = PositionMapper.GetPosition(text, span.Start + span.Length);
        return new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
            start.line, start.character, end.line, end.character);
    }
}
