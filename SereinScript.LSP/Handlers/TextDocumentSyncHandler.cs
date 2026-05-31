using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using SereinScript.LSP;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SereinScript.LSP.Handlers
{
    /// <summary>
    /// 文本文档同步处理器
    /// </summary>
    public class TextDocumentSyncHandler : ITextDocumentSyncHandler
    {
        private readonly DocumentManager _documentManager;
        private readonly DiagnosticsHandler _diagnosticsHandler;
        private readonly Dictionary<string, int> _documentVersions = new();
        private readonly Dictionary<string, CancellationTokenSource> _debounceTokens = new();
        private const int DebounceDelay = 300; // 防抖延迟，单位毫秒

        public TextDocumentSyncHandler(DocumentManager documentManager, DiagnosticsHandler diagnosticsHandler)
        {
            _documentManager = documentManager;
            _diagnosticsHandler = diagnosticsHandler;
        }

       /* /// <summary>
        /// 处理文档变更
        /// </summary>
        public async Task Handle(DidChangeTextDocumentParams notification, CancellationToken cancellationToken)
        {
            var uri = notification.TextDocument.Uri.ToString();
            var content = notification.ContentChanges.First().Text;
            var version = notification.TextDocument.Version;
            await HandleDocumentChangeAsync(uri, content, version);
        }
*/
/*        /// <summary>
        /// 处理文档打开
        /// </summary>
        public async Task Handle(DidOpenTextDocumentParams notification, CancellationToken cancellationToken)
        {
            var uri = notification.TextDocument.Uri.ToString();
            var content = notification.TextDocument.Text;
            var version = notification.TextDocument.Version;
            await HandleDocumentOpenAsync(uri, content, version);
        }*/

/*        /// <summary>
        /// 处理文档关闭
        /// </summary>
        public async Task Handle(DidCloseTextDocumentParams notification, CancellationToken cancellationToken)
        {
            var uri = notification.TextDocument.Uri.ToString();
            await HandleDocumentCloseAsync(uri);
        }*/
/*
        /// <summary>
        /// 处理文档保存
        /// </summary>
        public async Task Handle(DidSaveTextDocumentParams notification, CancellationToken cancellationToken)
        {
            var uri = notification.TextDocument.Uri.ToString();
            await HandleDocumentSaveAsync(uri);
        }
*/



        /// <summary>
        /// 处理文档变更
        /// </summary>
        public async Task HandleDocumentChangeAsync(string uri, string content, int? version = null)
        {
            Logger.Info($"Handling document change for: {uri}, Version: {version?.ToString() ?? "N/A"}", "TextDocumentSync");
            try
            {
                // 更新文档版本
                if (version.HasValue)
                {
                    _documentVersions[uri] = version.Value;
                    Logger.Debug($"Updated document version: {version.Value}", "TextDocumentSync");
                }
                else
                {
                    // 如果没有版本号，生成一个递增的版本号
                    if (!_documentVersions.ContainsKey(uri))
                        _documentVersions[uri] = 0;
                    _documentVersions[uri]++;
                    Logger.Debug($"Generated document version: {_documentVersions[uri]}", "TextDocumentSync");
                }

                // 更新文档内容
                _documentManager.UpdateDocumentContent(uri, content);
                Logger.Debug($"Updated document content, length: {content.Length}", "TextDocumentSync");
                
                // 触发语法检查（带防抖）
                await TriggerDiagnosticsWithDebounceAsync(uri);
                Logger.Info("Document change handled successfully", "TextDocumentSync");
            }
            catch (Exception ex)
            {
                // 记录详细错误
                Logger.Error($"Error handling document change: {ex.Message}", "TextDocumentSync");
                Logger.Error($"Error stack trace: {ex.StackTrace}", "TextDocumentSync");
            }
        }

        /// <summary>
        /// 处理文档打开
        /// </summary>
        public async Task HandleDocumentOpenAsync(string uri, string content, int? version = null)
        {
            Logger.Info($"Handling document open for: {uri}", "TextDocumentSync");
            try
            {
                // 更新文档版本
                if (version.HasValue)
                {
                    _documentVersions[uri] = version.Value;
                    Logger.Debug($"Set document version: {version.Value}", "TextDocumentSync");
                }
                else
                {
                    _documentVersions[uri] = 1;
                    Logger.Debug("Set document version: 1", "TextDocumentSync");
                }

                // 存储新打开的文档
                _documentManager.UpdateDocumentContent(uri, content);
                Logger.Debug($"Updated document content, length: {content.Length}", "TextDocumentSync");
                
                // 触发语法检查
                await _diagnosticsHandler.PublishDiagnosticsAsync(uri);
                Logger.Info("Document open handled successfully", "TextDocumentSync");
            }
            catch (Exception ex)
            {
                // 记录详细错误
                Logger.Error($"Error handling document open: {ex.Message}", "TextDocumentSync");
                Logger.Error($"Error stack trace: {ex.StackTrace}", "TextDocumentSync");
            }
        }

        /// <summary>
        /// 处理文档关闭
        /// </summary>
        public async Task HandleDocumentCloseAsync(string uri)
        {
            Logger.Info($"Handling document close for: {uri}", "TextDocumentSync");
            try
            {
                // 取消可能的防抖任务
                CancelDebounceTask(uri);
                Logger.Debug("Canceled debounce task", "TextDocumentSync");

                // 移除关闭的文档
                _documentManager.RemoveDocument(uri);
                Logger.Debug("Removed document from manager", "TextDocumentSync");
                
                // 清理文档版本记录
                _documentVersions.Remove(uri);
                Logger.Debug("Removed document version record", "TextDocumentSync");
                Logger.Info("Document close handled successfully", "TextDocumentSync");
            }
            catch (Exception ex)
            {
                // 记录详细错误
                Logger.Error($"Error handling document close: {ex.Message}", "TextDocumentSync");
                Logger.Error($"Error stack trace: {ex.StackTrace}", "TextDocumentSync");
            }
        }

        /// <summary>
        /// 处理文档保存
        /// </summary>
        public async Task HandleDocumentSaveAsync(string uri)
        {
            Logger.Info($"Handling document save for: {uri}", "TextDocumentSync");
            try
            {
                // 触发语法检查
                await _diagnosticsHandler.PublishDiagnosticsAsync(uri);
                Logger.Info("Document save handled successfully", "TextDocumentSync");
            }
            catch (Exception ex)
            {
                // 记录详细错误
                Logger.Error($"Error handling document save: {ex.Message}", "TextDocumentSync");
                Logger.Error($"Error stack trace: {ex.StackTrace}", "TextDocumentSync");
            }
        }

        /// <summary>
        /// 带防抖的诊断触发
        /// </summary>
        private async Task TriggerDiagnosticsWithDebounceAsync(string uri)
        {
            Logger.Debug($"Triggering diagnostics with debounce for: {uri}", "TextDocumentSync");
            try
            {
                // 取消之前的防抖任务
                CancelDebounceTask(uri);
                Logger.Debug("Canceled previous debounce task", "TextDocumentSync");

                // 创建新的取消令牌
                var cts = new CancellationTokenSource();
                _debounceTokens[uri] = cts;
                Logger.Debug($"Created new debounce task with delay: {DebounceDelay}ms", "TextDocumentSync");

                // 延迟执行
                await Task.Delay(DebounceDelay, cts.Token);

                // 检查是否被取消
                if (cts.IsCancellationRequested)
                {
                    Logger.Debug("Debounce task canceled", "TextDocumentSync");
                    return;
                }

                // 执行诊断
                await _diagnosticsHandler.PublishDiagnosticsAsync(uri);
                Logger.Debug("Debounce task executed diagnostics", "TextDocumentSync");
            }
            catch (TaskCanceledException)
            {
                // 任务被取消，这是正常的防抖行为
                Logger.Debug("Debounce task canceled (expected)", "TextDocumentSync");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in debounce diagnostics: {ex.Message}", "TextDocumentSync");
            }
            finally
            {
                // 清理取消令牌
                _debounceTokens.Remove(uri);
                Logger.Debug("Cleaned up debounce token", "TextDocumentSync");
            }
        }

        /// <summary>
        /// 取消防抖任务
        /// </summary>
        private void CancelDebounceTask(string uri)
        {
            if (_debounceTokens.TryGetValue(uri, out var cts))
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch { }
                _debounceTokens.Remove(uri);
            }
        }

        /// <summary>
        /// 获取文档版本
        /// </summary>
        public int GetDocumentVersion(string uri)
        {
            return _documentVersions.TryGetValue(uri, out var version) ? version : 0;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            // 取消所有防抖任务
            foreach (var token in _debounceTokens.Values)
            {
                try
                {
                    token.Cancel();
                    token.Dispose();
                }
                catch { }
            }
            _debounceTokens.Clear();
            _documentVersions.Clear();
        }



        /// <summary>
        /// 处理文档打开
        /// </summary>
        async Task<Unit> IRequestHandler<DidOpenTextDocumentParams, Unit>.Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToString();
            var content = request.TextDocument.Text;
            var version = request.TextDocument.Version;
            await HandleDocumentOpenAsync(uri, content, version);
            return Unit.Value;
        }

        /// <summary>
        /// 处理文档关闭
        /// </summary>
        async Task<Unit> IRequestHandler<DidCloseTextDocumentParams, Unit>.Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToString();
            await HandleDocumentCloseAsync(uri);
            return Unit.Value;
        }

        /// <summary>
        /// 处理文档变更
        /// </summary>

        async Task<Unit> IRequestHandler<DidChangeTextDocumentParams, Unit>.Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {

            var uri = request.TextDocument.Uri.ToString();
            var content = request.ContentChanges.First().Text;
            var version = request.TextDocument.Version;
            await HandleDocumentChangeAsync(uri, content, version);
            return Unit.Value;
        }


        /// <summary>
        /// 处理文档保存
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>

        async Task<Unit> IRequestHandler<DidSaveTextDocumentParams, Unit>.Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToString();
            await HandleDocumentSaveAsync(uri);
            return Unit.Value;

        }


        /// <summary>
        /// 当前使用 ContentChanges.First().Text
        /// 必须声明 TextDocumentSyncKind.Full
        /// </summary>
        /// <param name="capability"></param>
        /// <param name="clientCapabilities"></param>
        /// <returns></returns>

        public TextDocumentChangeRegistrationOptions GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentChangeRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(
                    new TextDocumentFilter
                    {
                        Language = "sereinscript",
                        Scheme = "file"
                    }
                ),
                SyncKind = TextDocumentSyncKind.Full
            };
        }


        TextDocumentOpenRegistrationOptions IRegistration<TextDocumentOpenRegistrationOptions, TextSynchronizationCapability>.GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentOpenRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(
                    new TextDocumentFilter
                    {
                        Language = "sereinscript",
                        Scheme = "file"
                    }
                ),
            };
        }


        TextDocumentCloseRegistrationOptions IRegistration<TextDocumentCloseRegistrationOptions, TextSynchronizationCapability>.GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentCloseRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(
                    new TextDocumentFilter
                    {
                        Language = "sereinscript",
                        Scheme = "file"
                    }
                ),
            };
        }


        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions, TextSynchronizationCapability>.GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentSaveRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(
                    new TextDocumentFilter
                    {
                        Language = "sereinscript",
                        Scheme = "file"
                    }
                ),
                IncludeText = false
            };
        }

        public TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        {
            return new TextDocumentAttributes(uri, "sereinscript");
        }
    }
}
