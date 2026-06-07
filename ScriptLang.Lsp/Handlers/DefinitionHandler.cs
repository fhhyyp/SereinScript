using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ScriptLang.Lsp.Analysis;
using ScriptLang.Lsp.Utilities;
using ScriptLang.Lsp.Workspace;
using ScriptLang.Parser;

namespace ScriptLang.Lsp.Handlers;

/// <summary>
/// 跳转定义处理器 — 将标识符引用跳转到其声明位置
/// </summary>
public sealed class DefinitionHandler : IDefinitionHandler
{
    private readonly WorkspaceManager _workspace;
    private readonly SymbolTable _symbolTable;

    public DefinitionHandler(WorkspaceManager workspace, SymbolTable symbolTable)
    {
        _workspace = workspace;
        _symbolTable = symbolTable;
    }

    public DefinitionRegistrationOptions GetRegistrationOptions(
        DefinitionCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DefinitionRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter { Pattern = "**/*.script" })
        };
    }

    public Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine($"[LSP.Def] uri={request.TextDocument.Uri} pos={request.Position.Line}:{request.Position.Character}");
        var doc = _workspace.GetDocument(request.TextDocument.Uri);
        if (doc?.Ast == null || doc.RootScope == null)
            return Task.FromResult<LocationOrLocationLinks?>(null);

        int offset = doc.GetOffset((int)request.Position.Line, (int)request.Position.Character);

        // 查找光标处的 AST 节点
        var node = PositionMapper.FindNodeAt(doc.Ast, offset);
        if (node == null)
            return Task.FromResult<LocationOrLocationLinks?>(null);

        // 获取符号信息
        SymbolInfo? symbol = null;

        if (node is IdentifierExpr id)
        {
            var scope = ScopeResolver.FindScopeAt(doc.RootScope, offset);
            symbol = scope?.Lookup(id.Name);
        }
        else if (node is LetExpr let)
        {
            var scope = ScopeResolver.FindScopeAt(doc.RootScope, offset);
            symbol = scope?.Lookup(let.Name);
        }
        else if (node is VarExpr var)
        {
            var scope = ScopeResolver.FindScopeAt(doc.RootScope, offset);
            symbol = scope?.Lookup(var.Name);
        }

        if (symbol == null)
            return Task.FromResult<LocationOrLocationLinks?>(null);

        // 转换为 LSP Location
        var start = PositionMapper.GetPosition(doc.Text, symbol.SourceSpan.Start);
        var end = PositionMapper.GetPosition(doc.Text, symbol.SourceSpan.Start + symbol.SourceSpan.Length);

        var location = new LocationOrLocationLink(new Location
        {
            Uri = doc.Uri,
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                start.line, start.character, end.line, end.character)
        });

        return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(location));
    }
}
