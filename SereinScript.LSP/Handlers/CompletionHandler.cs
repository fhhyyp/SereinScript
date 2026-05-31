using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ScriptLang.Lexer;
using ScriptLang.Parser;
using SereinScript.LSP;

namespace SereinScript.LSP.Handlers
{
    /// <summary>
    /// 自动补全处理器
    /// </summary>
    public class CompletionHandler : ICompletionHandler
    {
        private readonly DocumentManager _documentManager;
        private readonly SamplesParser _samplesParser;
        private readonly SyntaxAnalyzer _syntaxAnalyzer;
        private DSLSyntaxInfo? _syntaxInfo;

        public CompletionHandler(DocumentManager documentManager, SamplesParser samplesParser, SyntaxAnalyzer syntaxAnalyzer)
        {
            _documentManager = documentManager;
            _samplesParser = samplesParser;
            _syntaxAnalyzer = syntaxAnalyzer;
            _syntaxInfo = null;
        }


        public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CompletionRegistrationOptions
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


        public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToString();
            var position = request.Position;
            var completions = await GetCompletionsAsync(uri, position);
            return new CompletionList(completions, false);
        }

        public async Task InitializeAsync()
        {
            Logger.Info("Initializing completion handler", "Completion");
            try
            {
                _syntaxInfo = await _samplesParser.ParseSamplesAsync();
                Logger.Info("Completion handler initialized successfully", "Completion");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error initializing completion handler: {ex.Message}", "Completion");
            }
        }

        /// <summary>
        /// 生成自动补全建议
        /// </summary>
        /// <param name="uri">文档 URI</param>
        /// <param name="position">光标位置</param>
        /// <returns>补全建议列表</returns>
        public async Task<List<CompletionItem>> GetCompletionsAsync(string uri, Position position)
        {
            Logger.Info($"Getting completions for: {uri} at position {position.Line}:{position.Character}", "Completion");
            var completions = new List<CompletionItem>();
            var content = _documentManager.GetDocumentContent(uri);

            if (string.IsNullOrEmpty(content))
            {
                Logger.Debug("Empty document content, returning empty completions", "Completion");
                return completions;
            }

            // 获取当前行的文本
            var lines = content.Split('\n');
            if (position.Line >= lines.Length)
            {
                Logger.Debug("Position out of range, returning empty completions", "Completion");
                return completions;
            }

            var currentLine = lines[position.Line];
            var prefix = currentLine.Substring(0, position.Character);
            Logger.Debug($"Current prefix: '{prefix}'", "Completion");

            // 使用 SyntaxAnalyzer 进行语法分析，获取上下文信息
            var analysisResult = _syntaxAnalyzer.PerformSyntaxAnalysis(content, uri);

            // 基于 AST 生成更智能的补全建议
            if (analysisResult.Success && analysisResult.Ast != null)
            {
                Logger.Debug("Generating AST-based completions", "Completion");
                GenerateAstBasedCompletions(analysisResult.Ast, prefix, position, completions, uri);
            }
            else
            {
                // 如果语法分析失败，使用基于前缀的补全
                Logger.Warning("Syntax analysis failed, using prefix-based completions", "Completion");
                GenerateKeywordCompletions(prefix, completions);
                GenerateFunctionCompletions(prefix, completions);
                GenerateVariableCompletions(prefix, completions);
                GenerateSyntaxPatternCompletions(prefix, completions);
            }

            // 去重并排序
            var result = RemoveDuplicatesAndSort(completions, prefix);
            Logger.Info($"Generated {result.Count} completions", "Completion");
            return result;
        }

        /// <summary>
        /// 基于 AST 生成智能补全建议
        /// </summary>
        private void GenerateAstBasedCompletions(Expr ast, string prefix, Position position, List<CompletionItem> completions, string uri)
        {
            // 提取当前作用域中的变量和函数
            var localVariables = new List<string>();
            var localFunctions = new List<(string Name, List<string> Params)>();
            var context = GetCompletionContext(prefix, position, ast);
            
            // 从 AST 中提取可用的符号
            ExtractSymbolsFromAst(ast, localVariables, localFunctions, position);

            // 生成变量补全
            foreach (var variable in localVariables)
            {
                if (IsMatch(variable, prefix))
                {
                    completions.Add(new CompletionItem
                    {
                        Label = variable,
                        Kind = CompletionItemKind.Variable,
                        InsertText = variable,
                        Documentation = "局部变量",
                        SortText = $"0{variable}" // 优先显示局部变量
                    });
                }
            }

            // 生成函数补全
            foreach (var (name, parameters) in localFunctions)
            {
                if (IsMatch(name, prefix))
                {
                    var insertText = GenerateFunctionInsertText(name, parameters);
                    completions.Add(new CompletionItem
                    {
                        Label = name,
                        Kind = CompletionItemKind.Function,
                        InsertText = insertText,
                        Documentation = $"函数: {name}({string.Join(", ", parameters)})",
                        SortText = $"1{name}" // 其次显示局部函数
                    });
                }
            }

            // 基于上下文生成补全
            switch (context)
            {
                case CompletionContext.InsideFunctionCall:
                    // 生成函数参数补全
                    GenerateFunctionParameterCompletions(prefix, ast, position, completions);
                    break;
                case CompletionContext.AfterDot:
                    // 生成成员访问补全
                    GenerateMemberAccessCompletions(prefix, ast, position, completions);
                    break;
                case CompletionContext.InsideObjectLiteral:
                    // 生成对象属性补全
                    GenerateObjectPropertyCompletions(prefix, completions);
                    break;
                case CompletionContext.InsideArrayLiteral:
                    // 生成数组元素补全
                    GenerateArrayElementCompletions(prefix, completions);
                    break;
                case CompletionContext.InsideImport:
                    // 生成导入语句补全
                    GenerateImportCompletions(prefix, completions);
                    break;
                default:
                    // 生成关键字补全
                    GenerateKeywordCompletions(prefix, completions);
                    // 生成内置函数补全
                    GenerateBuiltinFunctionCompletions(prefix, completions);
                    // 生成语法模式补全
                    GenerateSyntaxPatternCompletions(prefix, completions);
                    break;
            }
        }

        /// <summary>
        /// 获取补全上下文
        /// </summary>
        private CompletionContext GetCompletionContext(string prefix, Position position, Expr ast)
        {
            // 检查是否在函数调用内部
            if (prefix.Contains('(') && !prefix.Contains(')'))
            {
                return CompletionContext.InsideFunctionCall;
            }

            // 检查是否在点号后面
            if (prefix.EndsWith("."))
            {
                return CompletionContext.AfterDot;
            }

            // 检查是否在对象字面量内部
            if (prefix.Contains('{') && !prefix.Contains('}'))
            {
                return CompletionContext.InsideObjectLiteral;
            }

            // 检查是否在数组字面量内部
            if (prefix.Contains('[') && !prefix.Contains(']'))
            {
                return CompletionContext.InsideArrayLiteral;
            }

            // 检查是否在import语句内部
            if (prefix.TrimStart().StartsWith("import"))
            {
                return CompletionContext.InsideImport;
            }

            return CompletionContext.Default;
        }

        /// <summary>
        /// 从 AST 中提取符号
        /// </summary>
        private void ExtractSymbolsFromAst(object ast, List<string> variables, List<(string Name, List<string> Params)> functions, Position position)
        {
            if (ast is ScriptLang.Parser.Program program)
            {
                foreach (var statement in program.Statements)
                {
                    // 只处理光标位置之前的语句
                    if (statement.SourceSpan.StartLine <= position.Line + 1)
                    {
                        ExtractSymbolsFromExpr(statement, variables, functions, position);
                    }
                }
            }
            else if (ast is Expr expr)
            {
                ExtractSymbolsFromExpr(expr, variables, functions, position);
            }
        }

        /// <summary>
        /// 从表达式中提取符号
        /// </summary>
        private void ExtractSymbolsFromExpr(Expr expr, List<string> variables, List<(string Name, List<string> Params)> functions, Position position)
        {
            switch (expr)
            {
                case LetExpr letExpr:
                    // 提取变量声明
                    if (!variables.Contains(letExpr.Name) && letExpr.SourceSpan.StartLine <= position.Line + 1)
                    {
                        variables.Add(letExpr.Name);
                    }
                    // 检查是否是函数声明（lambda 表达式）
                    if (letExpr.Value is LambdaExpr lambdaExpr)
                    {
                        if (!functions.Exists(f => f.Name == letExpr.Name) && letExpr.SourceSpan.StartLine <= position.Line + 1)
                        {
                            functions.Add((letExpr.Name, lambdaExpr.Params));
                        }
                    }
                    // 递归处理值表达式
                    ExtractSymbolsFromExpr(letExpr.Value, variables, functions, position);
                    break;

                case VarExpr varExpr:
                    // 提取变量声明
                    if (!variables.Contains(varExpr.Name) && varExpr.SourceSpan.StartLine <= position.Line + 1)
                    {
                        variables.Add(varExpr.Name);
                    }
                    // 检查是否是函数声明（lambda 表达式）
                    if (varExpr.Value is LambdaExpr varLambdaExpr)
                    {
                        if (!functions.Exists(f => f.Name == varExpr.Name) && varExpr.SourceSpan.StartLine <= position.Line + 1)
                        {
                            functions.Add((varExpr.Name, varLambdaExpr.Params));
                        }
                    }
                    // 递归处理值表达式
                    ExtractSymbolsFromExpr(varExpr.Value, variables, functions, position);
                    break;

                case BlockExpr blockExpr:
                    // 递归处理代码块中的所有语句
                    foreach (var statement in blockExpr.Statements)
                    {
                        if (statement.SourceSpan.StartLine <= position.Line + 1)
                        {
                            ExtractSymbolsFromExpr(statement, variables, functions, position);
                        }
                    }
                    break;

                case ForExpr forExpr:
                    // 提取循环变量
                    if (!variables.Contains(forExpr.VarName) && forExpr.SourceSpan.StartLine <= position.Line + 1)
                    {
                        variables.Add(forExpr.VarName);
                    }
                    // 递归处理迭代器和循环体
                    ExtractSymbolsFromExpr(forExpr.Iterable, variables, functions, position);
                    ExtractSymbolsFromExpr(forExpr.Body, variables, functions, position);
                    break;

                case ObjectLiteralExpr objectExpr:
                    // 递归处理对象属性
                    foreach (var property in objectExpr.Properties)
                    {
                        ExtractSymbolsFromExpr(property.Value, variables, functions, position);
                    }
                    break;

                case IfExpr ifExpr:
                    // 递归处理条件表达式
                    ExtractSymbolsFromExpr(ifExpr.Cond, variables, functions, position);
                    ExtractSymbolsFromExpr(ifExpr.Then, variables, functions, position);
                    ExtractSymbolsFromExpr(ifExpr.Else, variables, functions, position);
                    break;

                // 其他表达式类型的处理
                case BinaryExpr binaryExpr:
                    ExtractSymbolsFromExpr(binaryExpr.Left, variables, functions, position);
                    ExtractSymbolsFromExpr(binaryExpr.Right, variables, functions, position);
                    break;

                case CallExpr callExpr:
                    ExtractSymbolsFromExpr(callExpr.Target, variables, functions, position);
                    foreach (var arg in callExpr.Args)
                    {
                        ExtractSymbolsFromExpr(arg, variables, functions, position);
                    }
                    break;

                case MemberAccessExpr memberExpr:
                    ExtractSymbolsFromExpr(memberExpr.Target, variables, functions, position);
                    break;

                case IndexAccessExpr indexExpr:
                    ExtractSymbolsFromExpr(indexExpr.Target, variables, functions, position);
                    ExtractSymbolsFromExpr(indexExpr.Index, variables, functions, position);
                    break;

                case ArrayLiteralExpr arrayExpr:
                    foreach (var element in arrayExpr.Elements)
                    {
                        ExtractSymbolsFromExpr(element, variables, functions, position);
                    }
                    break;

                case ConditionalExpr conditionalExpr:
                    ExtractSymbolsFromExpr(conditionalExpr.Cond, variables, functions, position);
                    ExtractSymbolsFromExpr(conditionalExpr.Then, variables, functions, position);
                    ExtractSymbolsFromExpr(conditionalExpr.Else, variables, functions, position);
                    break;

                case UnaryExpr unaryExpr:
                    ExtractSymbolsFromExpr(unaryExpr.Expr, variables, functions, position);
                    break;

                case ReturnExpr returnExpr:
                    if (returnExpr.Value != null)
                    {
                        ExtractSymbolsFromExpr(returnExpr.Value, variables, functions, position);
                    }
                    break;

                case WhenExpr whenExpr:
                    ExtractSymbolsFromExpr(whenExpr.Value, variables, functions, position);
                    foreach (var clause in whenExpr.Clauses)
                    {
                        ExtractSymbolsFromExpr(clause.Pattern, variables, functions, position);
                        ExtractSymbolsFromExpr(clause.Body, variables, functions, position);
                    }
                    break;

                case LiteralExpr:
                case IdentifierExpr:
                    break;

                case LambdaExpr lambdaExprObj:
                    // 提取lambda表达式参数
                    foreach (var param in lambdaExprObj.Params)
                    {
                        if (!variables.Contains(param) && lambdaExprObj.SourceSpan.StartLine <= position.Line + 1)
                        {
                            variables.Add(param);
                        }
                    }
                    // 递归处理lambda表达式体
                    ExtractSymbolsFromExpr(lambdaExprObj.Body, variables, functions, position);
                    break;

                case AssignExpr assignExpr:
                    // 提取赋值目标
                    if (!variables.Contains(assignExpr.Name) && assignExpr.SourceSpan.StartLine <= position.Line + 1)
                    {
                        variables.Add(assignExpr.Name);
                    }
                    // 递归处理值表达式
                    ExtractSymbolsFromExpr(assignExpr.Value, variables, functions, position);
                    break;

                case MemberAssignExpr memberAssignExpr:
                    // 递归处理目标和值表达式
                    ExtractSymbolsFromExpr(memberAssignExpr.Target, variables, functions, position);
                    ExtractSymbolsFromExpr(memberAssignExpr.Value, variables, functions, position);
                    break;

                case IndexAssignExpr indexAssignExpr:
                    // 递归处理目标、索引和值表达式
                    ExtractSymbolsFromExpr(indexAssignExpr.Target, variables, functions, position);
                    ExtractSymbolsFromExpr(indexAssignExpr.Index, variables, functions, position);
                    ExtractSymbolsFromExpr(indexAssignExpr.Value, variables, functions, position);
                    break;

                case ImportStmt importStmt:
                    // 提取导入的成员
                    foreach (var member in importStmt.Members)
                    {
                        if (!variables.Contains(member) && importStmt.SourceSpan.StartLine <= position.Line + 1)
                        {
                            variables.Add(member);
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// 生成函数参数的插入文本
        /// </summary>
        private string GenerateFunctionInsertText(string functionName, List<string> parameters)
        {
            if (parameters.Count == 0)
            {
                return $"{functionName}()";
            }

            var paramPlaceholders = new List<string>();
            for (int i = 0; i < parameters.Count; i++)
            {
                paramPlaceholders.Add($"${{{i + 1}:{parameters[i]}}}");
            }

            var paramsText = string.Join(", ", paramPlaceholders);
            return $"{functionName}({paramsText})";
        }

        /// <summary>
        /// 生成函数参数补全
        /// </summary>
        private void GenerateFunctionParameterCompletions(string prefix, Expr ast, Position position, List<CompletionItem> completions)
        {
            // 这里可以根据函数调用的上下文生成参数补全
            // 暂时实现一个简单版本
        }

        /// <summary>
        /// 生成成员访问补全
        /// </summary>
        private void GenerateMemberAccessCompletions(string prefix, Expr ast, Position position, List<CompletionItem> completions)
        {
            // 提取点号前的标识符
            var identifier = prefix.Substring(0, prefix.LastIndexOf('.')).Trim();
            
            // 生成常见的对象成员
            var commonMembers = new[] { "length", "toString", "valueOf", "push", "pop", "forEach", "map", "filter" };
            foreach (var member in commonMembers)
            {
                completions.Add(new CompletionItem
                {
                    Label = member,
                    Kind = CompletionItemKind.Property,
                    InsertText = member,
                    Documentation = "对象成员"
                });
            }
        }

        /// <summary>
        /// 生成对象属性补全
        /// </summary>
        private void GenerateObjectPropertyCompletions(string prefix, List<CompletionItem> completions)
        {
            // 生成常见的对象属性
            var commonProperties = new[] { "name", "value", "id", "type", "length", "items", "data", "config" };
            foreach (var property in commonProperties)
            {
                if (IsMatch(property, prefix))
                {
                    completions.Add(new CompletionItem
                    {
                        Label = property,
                        Kind = CompletionItemKind.Property,
                        InsertText = $"{property}: {{1:value}}",
                        Documentation = "对象属性"
                    });
                }
            }
        }

        /// <summary>
        /// 生成内置函数补全建议
        /// </summary>
        private void GenerateBuiltinFunctionCompletions(string prefix, List<CompletionItem> completions)
        {
            // 添加内置函数
            var builtinFunctions = new Dictionary<string, (string Params, string Docs)>
            {
                { "print", ("message", "打印输出") },
                { "println", ("message", "打印输出并换行") },
                { "assert", ("condition, message", "断言检查") },
                { "parseInt", ("value", "解析整数") },
                { "parseFloat", ("value", "解析浮点数") },
                { "toString", ("value", "转换为字符串") },
                { "typeof", ("value", "获取类型") },
                { "read", ("", "读取输入") },
                { "readLine", ("", "读取一行输入") }
            };

            foreach (var (func, (paramsText, docs)) in builtinFunctions)
            {
                if (IsMatch(func, prefix))
                {
                    var insertText = GenerateFunctionInsertText(func, paramsText.Split(", ").ToList());
                    completions.Add(new CompletionItem
                    {
                        Label = func,
                        Kind = CompletionItemKind.Function,
                        InsertText = insertText,
                        Documentation = docs,
                        SortText = $"2{func}" // 内置函数排在后面
                    });
                }
            }
        }

        /// <summary>
        /// 生成关键字补全建议
        /// </summary>
        private void GenerateKeywordCompletions(string prefix, List<CompletionItem> completions)
        {
            var keywords = new Dictionary<string, string>
            {
                { "let", "声明不可变变量" },
                { "var", "声明可变变量" },
                { "if", "条件语句" },
                { "else", "否则分支" },
                { "then", "then 关键字" },
                { "for", "循环语句" },
                { "in", "in 关键字" },
                { "return", "返回语句" },
                { "import", "导入模块" },
                { "from", "from 关键字" },
                { "true", "布尔值 true" },
                { "false", "布尔值 false" },
                { "null", "空值" },
                { "when", "模式匹配" }
            };

            foreach (var (keyword, docs) in keywords)
            {
                if (IsMatch(keyword, prefix))
                {
                    completions.Add(new CompletionItem
                    {
                        Label = keyword,
                        Kind = CompletionItemKind.Keyword,
                        InsertText = keyword,
                        Documentation = docs,
                        SortText = $"3{keyword}" // 关键字排在最后
                    });
                }
            }
        }

        /// <summary>
        /// 生成函数补全建议
        /// </summary>
        private void GenerateFunctionCompletions(string prefix, List<CompletionItem> completions)
        {
            // 从 Samples 分析结果中获取函数
            if (_syntaxInfo != null)
            {
                foreach (var function in _syntaxInfo.Functions)
                {
                    if (IsMatch(function.Name, prefix))
                    {
                        var insertText = GenerateFunctionInsertText(function.Name, function.Parameters);
                        completions.Add(new CompletionItem
                        {
                            Label = function.Name,
                            Kind = CompletionItemKind.Function,
                            InsertText = insertText,
                            Documentation = GetFunctionDocumentation(function),
                            SortText = $"1{function.Name}" // 示例函数排在局部函数之后
                        });
                    }
                }
            }

            // 添加内置函数
            GenerateBuiltinFunctionCompletions(prefix, completions);
        }

        /// <summary>
        /// 生成变量补全建议
        /// </summary>
        private void GenerateVariableCompletions(string prefix, List<CompletionItem> completions)
        {
            // 从 Samples 分析结果中获取变量
            if (_syntaxInfo != null)
            {
                foreach (var variable in _syntaxInfo.Variables)
                {
                    if (IsMatch(variable, prefix))
                    {
                        completions.Add(new CompletionItem
                        {
                            Label = variable,
                            Kind = CompletionItemKind.Variable,
                            InsertText = variable,
                            Documentation = "变量",
                            SortText = $"1{variable}" // 示例变量排在局部变量之后
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 生成数组元素补全
        /// </summary>
        private void GenerateArrayElementCompletions(string prefix, List<CompletionItem> completions)
        {
            // 生成常见的数组元素类型
            var commonElements = new[] { "1", "\"text\"", "true", "false", "null", "[]", "{}" };
            foreach (var element in commonElements)
            {
                completions.Add(new CompletionItem
                {
                    Label = element,
                    Kind = CompletionItemKind.Value,
                    InsertText = element,
                    Documentation = "数组元素"
                });
            }
        }

        /// <summary>
        /// 生成导入语句补全
        /// </summary>
        private void GenerateImportCompletions(string prefix, List<CompletionItem> completions)
        {
            // 生成导入语句模板
            completions.Add(new CompletionItem
            {
                Label = "import statement",
                Kind = CompletionItemKind.Snippet,
                InsertText = "import { ${1:members} } from \"${2:path}\"",
                Documentation = "导入语句",
                SortText = $"4import"
            });

            // 生成常见的导入路径
            var commonPaths = new[] { "utils", "helpers", "constants", "config", "models" };
            foreach (var path in commonPaths)
            {
                if (IsMatch(path, prefix))
                {
                    completions.Add(new CompletionItem
                    {
                        Label = path,
                        Kind = CompletionItemKind.File,
                        InsertText = path,
                        Documentation = "导入路径",
                        SortText = $"5{path}"
                    });
                }
            }
        }

        /// <summary>
        /// 生成语法模式补全建议
        /// </summary>
        private void GenerateSyntaxPatternCompletions(string prefix, List<CompletionItem> completions)
        {
            // 生成常用语法模式
            var trimmedPrefix = prefix.Trim();
            
            if (trimmedPrefix == "let")
            {
                completions.Add(new CompletionItem
                {
                    Label = "let variable = value",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "let ${1:variable} = ${2:value}",
                    Documentation = "变量声明",
                    SortText = $"4let"
                });
            }
            else if (trimmedPrefix == "var")
            {
                completions.Add(new CompletionItem
                {
                    Label = "var variable = value",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "var ${1:variable} = ${2:value}",
                    Documentation = "可变变量声明",
                    SortText = $"4var"
                });
            }
            else if (trimmedPrefix == "if")
            {
                completions.Add(new CompletionItem
                {
                    Label = "if condition",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "if (${1:condition}) {\n    ${2:// code}\n}",
                    Documentation = "条件语句",
                    SortText = $"4if"
                });
            }
            else if (trimmedPrefix == "for")
            {
                completions.Add(new CompletionItem
                {
                    Label = "for loop",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "for (${1:item} in ${2:iterable}) {\n    ${3:// code}\n}",
                    Documentation = "循环语句",
                    SortText = $"4for"
                });
            }
            else if (trimmedPrefix == "when")
            {
                completions.Add(new CompletionItem
                {
                    Label = "when pattern matching",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "when (${1:value}) {\n    ${2:pattern} => ${3:expression},\n    else => ${4:expression}\n}",
                    Documentation = "模式匹配语句",
                    SortText = $"4when"
                });
            }
            else if (trimmedPrefix == "return")
            {
                completions.Add(new CompletionItem
                {
                    Label = "return statement",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "return ${1:value}",
                    Documentation = "返回语句",
                    SortText = $"4return"
                });
            }
            else if (prefix.Contains("=>"))
            {
                completions.Add(new CompletionItem
                {
                    Label = "arrow function",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "(${1:parameters}) => ${2:expression}",
                    Documentation = "箭头函数",
                    SortText = $"4arrow"
                });
            }
            else if (prefix.Contains("{") && !prefix.Contains("}"))
            {
                completions.Add(new CompletionItem
                {
                    Label = "object literal",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "{\n    ${1:property}: ${2:value}\n}",
                    Documentation = "对象字面量",
                    SortText = $"4object"
                });
            }
            else if (prefix.Contains("[") && !prefix.Contains("]"))
            {
                completions.Add(new CompletionItem
                {
                    Label = "array literal",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "[${1:elements}]",
                    Documentation = "数组字面量",
                    SortText = $"4array"
                });
            }
        }

        /// <summary>
        /// 检查字符串是否匹配前缀
        /// </summary>
        private bool IsMatch(string text, string prefix)
        {
            // 移除前缀中的特殊字符
            var cleanPrefix = prefix.Split(' ').Last().Split('.').Last();
            return text.StartsWith(cleanPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 去重并排序补全项
        /// </summary>
        private List<CompletionItem> RemoveDuplicatesAndSort(List<CompletionItem> completions, string prefix)
        {
            // 按标签去重
            var uniqueCompletions = completions
                .GroupBy(c => c.Label)
                .Select(g => g.First())
                .ToList();

            // 按 SortText 排序，如果没有则按标签排序
            return uniqueCompletions
                .OrderBy(c => c.SortText ?? c.Label)
                .ToList();
        }

        /// <summary>
        /// 获取关键字文档
        /// </summary>
        private string GetKeywordDocumentation(string keyword)
        {
            var docs = new Dictionary<string, string>
            {
                { "let", "声明不可变变量" },
                { "var", "声明可变变量" },
                { "if", "条件语句" },
                { "else", "否则分支" },
                { "for", "循环语句" },
                { "in", "in 关键字" },
                { "return", "返回语句" },
                { "import", "导入模块" },
                { "from", "from 关键字" },
                { "true", "布尔值 true" },
                { "false", "布尔值 false" },
                { "null", "空值" },
                { "when", "模式匹配" }
            };

            return docs.TryGetValue(keyword, out var doc) ? doc : "关键字";
        }

        /// <summary>
        /// 获取函数文档
        /// </summary>
        private string GetFunctionDocumentation(DSLFunction function)
        {
            return $"函数: {function.Name}({string.Join(", ", function.Parameters)})";
        }

        /// <summary>
        /// 获取内置函数文档
        /// </summary>
        private string GetBuiltinFunctionDocumentation(string function)
        {
            var docs = new Dictionary<string, string>
            {
                { "print", "打印输出" },
                { "println", "打印输出并换行" },
                { "assert", "断言检查" },
                { "parseInt", "解析整数" },
                { "parseFloat", "解析浮点数" },
                { "toString", "转换为字符串" },
                { "typeof", "获取类型" },
                { "read", "读取输入" },
                { "readLine", "读取一行输入" }
            };

            return docs.TryGetValue(function, out var doc) ? doc : "内置函数";
        }

        /// <summary>
        /// 补全上下文类型
        /// </summary>
        private enum CompletionContext
        {
            Default,
            InsideFunctionCall,
            AfterDot,
            InsideObjectLiteral,
            InsideArrayLiteral,
            InsideImport
        }
    }
}

