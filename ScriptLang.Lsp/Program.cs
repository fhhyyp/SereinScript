using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Server;
using ScriptLang.Lsp.Analysis;
using ScriptLang.Lsp.Handlers;
using ScriptLang.Lsp.Workspace;

namespace ScriptLang.Lsp;

class Program
{
    static async Task Main(string[] args)
    {
        var server = await LanguageServer.From(
            options => options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .WithHandler<CompletionHandler>()
                .WithHandler<HoverHandler>()
                .WithHandler<DefinitionHandler>()
                .WithHandler<ReferencesHandler>()
                .WithHandler<DocumentSymbolHandler>()
                .WithServices(services =>
                {
                    services.AddSingleton(new WorkspaceManager());
                    services.AddSingleton(new SymbolTable());
                })
        );

        await server.WaitForExit;
    }
}
