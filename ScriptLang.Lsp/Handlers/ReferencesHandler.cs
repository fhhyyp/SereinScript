using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ScriptLang.Lsp.Analysis;
using ScriptLang.Lsp.Utilities;
using ScriptLang.Lsp.Workspace;
using ScriptLang.Parser;

namespace ScriptLang.Lsp.Handlers;

/// <summary>
/// 查找引用处理器 — 查找指定符号的所有引用位置
/// </summary>
public sealed class ReferencesHandler : IReferencesHandler
{
    private readonly WorkspaceManager _workspace;
    private readonly SymbolTable _symbolTable;

    public ReferencesHandler(WorkspaceManager workspace, SymbolTable symbolTable)
    {
        _workspace = workspace;
        _symbolTable = symbolTable;
    }

    public ReferenceRegistrationOptions GetRegistrationOptions(
        ReferenceCapability capability, ClientCapabilities clientCapabilities)
    {
        return new ReferenceRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter { Pattern = "**/*.script" })
        };
    }

    public Task<LocationContainer?> Handle(ReferenceParams request, CancellationToken cancellationToken)
    {
        var doc = _workspace.GetDocument(request.TextDocument.Uri);
        if (doc?.Ast == null || doc.RootScope == null)
            return Task.FromResult<LocationContainer?>(null);

        int offset = doc.GetOffset((int)request.Position.Line, (int)request.Position.Character);

        // 获取光标处的符号名
        string? targetName = GetSymbolNameAt(doc.Ast, offset);
        if (targetName == null)
            return Task.FromResult<LocationContainer?>(null);

        // 查找所有引用
        var references = _symbolTable.FindReferences(doc.RootScope, targetName, doc.Ast);

        var locations = new List<Location>();
        foreach (var span in references)
        {
            var start = PositionMapper.GetPosition(doc.Text, span.Start);
            var end = PositionMapper.GetPosition(doc.Text, span.Start + span.Length);

            locations.Add(new Location
            {
                Uri = doc.Uri,
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    start.line, start.character, end.line, end.character)
            });
        }

        return Task.FromResult<LocationContainer?>(new LocationContainer(locations));
    }

    /// <summary>
    /// 获取指定位置处的符号名称
    /// </summary>
    private static string? GetSymbolNameAt(Expr root, int offset)
    {
        var node = PositionMapper.FindNodeAt(root, offset);
        if (node == null) return null;

        return node switch
        {
            IdentifierExpr id => id.Name,
            LetExpr let => let.Name,
            VarExpr var => var.Name,
            _ => null
        };
    }
}
