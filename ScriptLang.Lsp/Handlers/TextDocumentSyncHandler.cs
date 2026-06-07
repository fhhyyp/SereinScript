using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using ScriptLang.Lsp.Analysis;
using ScriptLang.Lsp.Workspace;

namespace ScriptLang.Lsp.Handlers;

/// <summary>
/// 文档同步处理器 — 处理 didOpen/didChange/didClose，构建 AST 和符号表
/// </summary>
public sealed class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private readonly WorkspaceManager _workspace;

    public TextDocumentSyncHandler(WorkspaceManager workspace)
    {
        _workspace = workspace;
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine($"[LSP.Sync] didOpen: {request.TextDocument.Uri}");
        ModuleMemberProvider.ClearCache();
        _workspace.OpenOrUpdate(request.TextDocument.Uri, request.TextDocument.Text, request.TextDocument.Version ?? 1);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var text = request.ContentChanges.FirstOrDefault()?.Text ?? "";
        Console.Error.WriteLine($"[LSP.Sync] didChange: {request.TextDocument.Uri}");
        // 清除全部脚本导出缓存（跨 import 依赖链，任何变更都可能影响其他文件的成员推断）
        ModuleMemberProvider.ClearCache();
        _workspace.OpenOrUpdate(request.TextDocument.Uri, text, request.TextDocument.Version ?? 1);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine($"[LSP.Sync] didClose: {request.TextDocument.Uri}");
        _workspace.CloseDocument(request.TextDocument.Uri);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine($"[LSP.Sync] didSave: {request.TextDocument.Uri}");
        if (request.Text != null)
            _workspace.OpenOrUpdate(request.TextDocument.Uri, request.Text, 1);
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter { Pattern = "**/*.script" }),
        };
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "sereinscript");
    }
}
