using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ScriptLang.Lsp.Analysis;
using ScriptLang.Lsp.Workspace;
using ScriptLang.Parser;

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
        {
            Console.Error.WriteLine($"[LSP.Completion] SKIP: doc={doc != null}, ast={doc?.Ast != null}, scope={doc?.RootScope != null}");
            return Task.FromResult(new CompletionList());
        }

        int offset = doc.GetOffset((int)request.Position.Line, (int)request.Position.Character);
        var visibleSymbols = _symbolTable.GetVisibleSymbols(doc.RootScope, offset);
        var context = GetCompletionContext(doc.Text, offset, request.Context?.TriggerCharacter);

        Console.Error.WriteLine($"[LSP.Completion] pos={request.Position.Line}:{request.Position.Character} trigger={request.Context?.TriggerCharacter ?? "null"} context={context} symbols={visibleSymbols.Count}");

        var items = new List<CompletionItem>();

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

        Console.Error.WriteLine($"[LSP.Completion] returning {items.Count} items");
        return Task.FromResult(new CompletionList(items));
    }

    // ==================== 上下文判断 ====================

    private enum CompletionContext { Identifier, MemberAccess, ImportBlock }

    private static CompletionContext GetCompletionContext(string text, int offset, string? triggerChar)
    {
        if (triggerChar == ".")
            return CompletionContext.MemberAccess;

        // 兜底：检查光标前一个字符是否为 `.`（处理无 triggerChar 的补全请求）
        if (offset > 0 && text[offset - 1] == '.')
            return CompletionContext.MemberAccess;

        // 检查是否在 import { } 内部：找光标前最近的未闭合 `{` 是否属于 import
        if (IsInsideImportBraces(text, offset))
            return CompletionContext.ImportBlock;

        return CompletionContext.Identifier;
    }

    /// <summary>检查光标是否在 import { ... } 的未闭合大括号内</summary>
    private static bool IsInsideImportBraces(string text, int offset)
    {
        int braceDepth = 0;

        for (int i = Math.Min(offset, text.Length - 1); i >= 0; i--)
        {
            char c = text[i];
            if (c == '}') braceDepth++;
            else if (c == '{')
            {
                if (braceDepth > 0) { braceDepth--; continue; }
                // 找到未闭合的 {，检查前面是否有 import 关键字
                for (int j = i - 1; j >= Math.Max(0, i - 30); j--)
                {
                    if (char.IsLetter(text[j])) continue;
                    string word = text[Math.Max(0, j - 5)..(j + 1)].Trim();
                    if (word.EndsWith("import")) return true;
                    break;
                }
                return false;
            }
        }
        return false;
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
        // 尝试从光标上下文推断访问目标（如 `file.` → 查找 `file` 符号的模块成员）
        var targetSymbol = ResolveMemberTarget(doc, offset);
        bool hasModuleMembers = false;

        if (targetSymbol?.ModuleMembers is { Count: > 0 } members)
        {
            hasModuleMembers = true;
            foreach (var m in members)
            {
                items.Add(new CompletionItem
                {
                    Label = m.Name,
                    Kind = m.IsProperty ? CompletionItemKind.Property : CompletionItemKind.Method,
                    Detail = m.Description,
                    SortText = $"0_{m.Name}"
                });
            }
        }

        // 对于本地 let/var 变量，尝试从初始值推断类型并添加原型方法
        if (targetSymbol != null && targetSymbol.Kind is ScriptSymbolKind.LetVariable or ScriptSymbolKind.VarVariable)
        {
            var initExpr = doc.Ast is ProgramExpr p ? ModuleMemberProvider.FindVariableInit(p, targetSymbol.Name) : null;
            if (initExpr != null)
            {
                string typeName = ModuleMemberProvider.InferExprType(initExpr);
                if (typeName != "unknown")
                {
                    var protoMembers = ModuleMemberProvider.GetPrototypeMembers(typeName);
                    foreach (var m in protoMembers)
                    {
                        items.Add(new CompletionItem
                        {
                            Label = m.Name,
                            Kind = m.IsProperty ? CompletionItemKind.Property : CompletionItemKind.Method,
                            Detail = m.Description,
                            SortText = $"0_{m.Name}"
                        });
                    }
                }
            }
        }

        // 通用 fallback 成员（始终添加，排在最后）
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
                SortText = $"1_{name}"
            });
        }
    }

    /// <summary>
    /// 从 `.` 之前的文本推断访问目标符号
    /// 例如 `file.re|` → 找到 `file` 的 SymbolInfo
    /// </summary>
    private static SymbolInfo? ResolveMemberTarget(DocumentInfo doc, int offset)
    {
        // 向前查找最近的 `.` 位置（跳过当前字符和空白）
        int dotPos = offset - 1;
        while (dotPos >= 0 && doc.Text[dotPos] != '.')
            dotPos--;

        if (dotPos < 0) return null;

        // 找到 `.` 前的标识符起始位置
        int idStart = dotPos - 1;
        // 跳过 `.` 前的空白
        while (idStart >= 0 && char.IsWhiteSpace(doc.Text[idStart]))
            idStart--;
        // 扫描标识符（字母、数字、下划线）
        int idEnd = idStart + 1;
        while (idStart >= 0 && (char.IsLetterOrDigit(doc.Text[idStart]) || doc.Text[idStart] == '_'))
            idStart--;

        // idStart 现在指向标识符前一个字符，需要 +1
        string targetName = doc.Text[(idStart + 1)..idEnd];

        if (string.IsNullOrEmpty(targetName))
            return null;

        // 在当前作用域中查找该符号
        var scope = ScopeResolver.FindScopeAt(doc.RootScope!, idStart + 1);
        return scope?.Lookup(targetName);
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
