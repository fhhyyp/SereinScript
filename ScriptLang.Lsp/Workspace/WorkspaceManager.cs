using System.Collections.Concurrent;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace ScriptLang.Lsp.Workspace;

/// <summary>
/// 工作区管理器 — 管理所有打开的文档
/// </summary>
public sealed class WorkspaceManager
{
    private readonly ConcurrentDictionary<DocumentUri, DocumentInfo> _documents = new();

    /// <summary>
    /// 打开或更新文档
    /// </summary>
    public DocumentInfo OpenOrUpdate(DocumentUri uri, string text, int version)
    {
        var doc = _documents.AddOrUpdate(
            uri,
            _ => new DocumentInfo(uri.ToUri(), text, version),
            (_, existing) =>
            {
                existing.Update(text, version);
                return existing;
            });

        return doc;
    }

    /// <summary>
    /// 获取文档信息
    /// </summary>
    public DocumentInfo? GetDocument(DocumentUri uri)
    {
        _documents.TryGetValue(uri, out var doc);
        return doc;
    }

    /// <summary>
    /// 关闭文档
    /// </summary>
    public void CloseDocument(DocumentUri uri)
    {
        _documents.TryRemove(uri, out _);
    }
}
