using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using SereinScript.LSP.Handlers;
using System.Threading.Tasks;

namespace SereinScript.LSP
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            /* // 创建 LSP 服务构建器
             var server = await LanguageServer.From(options => options
                 .WithInput(Console.OpenStandardInput())
                 .WithOutput(Console.OpenStandardOutput())
                 .ConfigureLogging(x => x
                     .SetMinimumLevel(LogLevel.Information))
                 .WithServices(services =>
                 {
                     // 注册服务
                     services.AddSingleton<DocumentManager>();
                     services.AddSingleton<SyntaxAnalyzer>();
                     services.AddSingleton<SamplesParser>(provider => new SamplesParser(@"..\..\..\..\ScriptLang\Samples"));
                     services.AddSingleton<DiagnosticsHandler>();
                     services.AddSingleton<ITextDocumentSyncHandler, TextDocumentSyncHandler>();
                     services.AddSingleton<ICompletionHandler, CompletionHandler>();
                     services.AddSingleton<IDocumentSymbolHandler, DocumentSymbolHandler>();
                     services.AddSingleton<IDefinitionHandler, DefinitionHandler>();
                 }));

             // 初始化 Samples 解析
             var completionHandler = server.Services.GetRequiredService<CompletionHandler>();
             await completionHandler.InitializeAsync();

            // 启动服务

            await server.WaitForExit;*/

            var server = await LanguageServer.From(options => options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .ConfigureLogging(x => x.SetMinimumLevel(LogLevel.Information))
                .WithServices(services =>
                {
                    Console.WriteLine("注册处理器 - 文档管理器");
                    services.AddSingleton<DocumentManager>();

                    Console.WriteLine("注册处理器 - 语法分析");
                    services.AddSingleton<SyntaxAnalyzer>();

                    Console.WriteLine("注册处理器 - SamplesParser");
                    services.AddSingleton<SamplesParser>(provider =>
                    {
                        Console.WriteLine("创建 - SamplesParser");
                        return new SamplesParser(@"Samples");
                    });

                    Console.WriteLine("注册处理器 - 语法诊断");
                    services.AddSingleton<DiagnosticsHandler>();

                    Console.WriteLine("注册处理器 - 文档文本同步");
                    services.AddSingleton</*ITextDocumentSyncHandler,*/ TextDocumentSyncHandler>();

                    Console.WriteLine("注册处理器 - 自动补全");
                    services.AddSingleton</*ICompletionHandler, */CompletionHandler>();

                    Console.WriteLine("注册处理器 - 文档符号");
                    services.AddSingleton</*IDocumentSymbolHandler, */DocumentSymbolHandler>();

                    Console.WriteLine("注册处理器 - 跳转定义");
                    services.AddSingleton</*IDefinitionHandler, */DefinitionHandler>();
                })
            ).ConfigureAwait(false);
            Logger.Info("启动LSP");
            // 初始化 Completion
            var completionHandler = server.Services.GetRequiredService<CompletionHandler>();
            Logger.Info("初始化 Completion");

            await completionHandler.InitializeAsync().ConfigureAwait(false);
            Logger.Info("等待客户端推出");

            // 等待客户端退出
            await server.WaitForExit.ConfigureAwait(false);

            Logger.Info("客户端已退出，LSP停止运行");

        }
    }
}