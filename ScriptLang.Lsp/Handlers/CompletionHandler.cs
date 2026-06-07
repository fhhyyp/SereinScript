using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ScriptLang.Lsp.Analysis;
using ScriptLang.Lsp.Workspace;

namespace ScriptLang.Lsp.Handlers;

/// <summary>
/// 代码补全处理器 — 提供可见符号、关键字、代码片段的补全
/// </summary>
public sealed class CompletionHandler : ICompletionHandler
{
    private readonly WorkspaceManager _workspace;
    private readonly SymbolTable _symbolTable;

    public CompletionHandler(WorkspaceManager workspace, SymbolTable symbolTable)
    {
        _workspace = workspace;
        _symbolTable = symbolTable;
    }

    // ==================== 触发字符 ====================

    public CompletionRegistrationOptions GetRegistrationOptions(
        CompletionCapability capability, ClientCapabilities clientCapabilities)
    {
        return new CompletionRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter { Pattern = "**/*.script" }),
            TriggerCharacters = new Container<string>(".", " "),
            AllCommitCharacters = new Container<string>(" "),
            ResolveProvider = false
        };
    }

    // ==================== 补全逻辑 ====================

    public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        var doc = _workspace.GetDocument(request.TextDocument.Uri);
        if (doc?.Ast == null || doc.RootScope == null)
            return Task.FromResult(new CompletionList());

        // 1. 查当前作用域中的可见符号
        int offset = doc.GetOffset((int)request.Position.Line, (int)request.Position.Character);
        var visibleSymbols = _symbolTable.GetVisibleSymbols(doc.RootScope, offset);

        var items = new List<CompletionItem>();

        // 2. 确定上下文
        var context = GetCompletionContext(doc.Text, offset, request.Context?.TriggerCharacter);

        // 3. 按上下文补全
        switch (context)
        {
            case CompletionContext.MemberAccess:
                AddMemberCompletions(items, doc, offset);
                break;
            case CompletionContext.ImportBlock:
                AddImportCompletions(items);
                break;
            case CompletionContext.Identifier:
            default:
                AddSymbolCompletions(items, visibleSymbols);
                AddKeywordCompletions(items);
                AddSnippetCompletions(items);
                break;
        }

        return Task.FromResult(new CompletionList(items));
    }

    // ==================== 上下文判断 ====================

    private enum CompletionContext { Identifier, MemberAccess, ImportBlock }

    private static CompletionContext GetCompletionContext(string text, int offset, string? triggerChar)
    {
        if (triggerChar == ".")
            return CompletionContext.MemberAccess;

        // 检查是否在 import { } 内部
        int searchStart = Math.Max(0, offset - 100);
        string prefix = text[searchStart..Math.Min(offset, text.Length)];
        if (prefix.Contains("import") && prefix.Contains("{"))
            return CompletionContext.ImportBlock;

        return CompletionContext.Identifier;
    }

    // ==================== 符号补全 ====================

    private static void AddSymbolCompletions(List<CompletionItem> items, Dictionary<string, SymbolInfo> symbols)
    {
        foreach (var (name, symbol) in symbols)
        {
            items.Add(new CompletionItem
            {
                Label = name,
                Kind = ToCompletionItemKind(symbol),
                Detail = GetSymbolDetail(symbol),
                SortText = $"0_{name}"  // 符号优先于关键字
            });
        }
    }

    private static CompletionItemKind ToCompletionItemKind(SymbolInfo symbol) => symbol.Kind switch
    {
        ScriptSymbolKind.Function => CompletionItemKind.Function,
        ScriptSymbolKind.Builtin => CompletionItemKind.Function,
        ScriptSymbolKind.Import => CompletionItemKind.Module,
        ScriptSymbolKind.Parameter => CompletionItemKind.Variable,
        ScriptSymbolKind.LetVariable => CompletionItemKind.Constant,
        ScriptSymbolKind.VarVariable => CompletionItemKind.Variable,
        _ => CompletionItemKind.Text
    };

    private static string GetSymbolDetail(SymbolInfo symbol) => symbol.Kind switch
    {
        ScriptSymbolKind.LetVariable => $"(let) {symbol.Name}",
        ScriptSymbolKind.VarVariable => $"(var) {symbol.Name}",
        ScriptSymbolKind.Parameter => $"(param) {symbol.Name}",
        ScriptSymbolKind.Function => "(function)",
        ScriptSymbolKind.Import => symbol.Detail ?? "import",
        ScriptSymbolKind.Builtin => "(builtin)",
        _ => symbol.Name
    };

    // ==================== 关键字补全 ====================

    private static void AddKeywordCompletions(List<CompletionItem> items)
    {
        var keywords = new (string keyword, string description)[]
        {
            ("let", "声明不可变变量"),
            ("var", "声明可变变量"),
            ("if", "条件表达式"),
            ("then", "条件分支"),
            ("else", "条件否则分支"),
            ("when", "模式匹配"),
            ("for", "循环表达式"),
            ("in", "循环迭代器"),
            ("return", "返回语句"),
            ("import", "模块导入"),
            ("from", "导入来源"),
            ("true", "布尔真"),
            ("false", "布尔假"),
            ("null", "空值"),
        };

        foreach (var (keyword, desc) in keywords)
        {
            items.Add(new CompletionItem
            {
                Label = keyword,
                Kind = CompletionItemKind.Keyword,
                Detail = desc,
                SortText = $"1_{keyword}"
            });
        }
    }

    // ==================== 代码片段补全 ====================

    private static void AddSnippetCompletions(List<CompletionItem> items)
    {
        var snippets = new (string label, string insertText, string description)[]
        {
            ("let",    "let ${1:name} = ${2:value}",                      "声明不可变变量"),
            ("var",    "var ${1:name} = ${2:value}",                      "声明可变变量"),
            ("if",     "if ${1:condition} then ${2:body} else ${3:else}", "if-then-else 表达式"),
            ("when",   "when ${1:value} {\n    ${2:pattern} => ${3:body}\n}", "when 模式匹配"),
            ("for",    "for ${1:item} in ${2:iterable} {\n    ${3:body}\n}",  "for-in 循环"),
            ("func",   "(${1:params}) => {\n    ${2:body}\n}",            "Lambda 表达式"),
            ("import", "import { ${1:member} } from \"${2:path}\"",       "模块导入"),
            ("print",  "print(${1:value})",                               "打印输出"),
        };

        foreach (var (label, insertText, desc) in snippets)
        {
            items.Add(new CompletionItem
            {
                Label = label,
                Kind = CompletionItemKind.Snippet,
                Detail = desc,
                InsertText = insertText,
                InsertTextFormat = InsertTextFormat.Snippet,
                SortText = $"2_{label}"
            });
        }
    }

    // ==================== 成员访问补全 ====================

    private static void AddMemberCompletions(List<CompletionItem> items, DocumentInfo doc, int offset)
    {
        // 动态类型无法静态推导成员，返回系统内置成员的启发式补全
        var commonMembers = new (string name, string desc)[]
        {
            ("length", "字符串/数组长度"),
            ("has", "检查对象是否存在某个键"),
            ("keys", "获取对象所有键"),
            ("count", "元素数量"),
        };

        foreach (var (name, desc) in commonMembers)
        {
            items.Add(new CompletionItem
            {
                Label = name,
                Kind = CompletionItemKind.Property,
                Detail = desc,
                SortText = $"0_{name}"
            });
        }
    }

    // ==================== Import 补全 ====================

    private static void AddImportCompletions(List<CompletionItem> items)
    {
        var modules = new[] { "file", "console", "path", "json", "network", "timer", "crypto", "process" };
        foreach (var mod in modules)
        {
            items.Add(new CompletionItem
            {
                Label = mod,
                Kind = CompletionItemKind.Module,
                Detail = $"system/{mod}",
                SortText = $"0_{mod}"
            });
        }
    }
}
