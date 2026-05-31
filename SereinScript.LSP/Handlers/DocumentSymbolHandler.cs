using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using ScriptLang.Parser;
using SereinScript.LSP;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace SereinScript.LSP.Handlers
{
    /// <summary>
    /// 文档符号处理器
    /// </summary>
    public class DocumentSymbolHandler : IDocumentSymbolHandler
    {
        private readonly DocumentManager _documentManager;
        private readonly SyntaxAnalyzer _syntaxAnalyzer;

        public DocumentSymbolHandler(DocumentManager documentManager, SyntaxAnalyzer syntaxAnalyzer)
        {
            _documentManager = documentManager;
            _syntaxAnalyzer = syntaxAnalyzer;
        }

        public DocumentSymbolRegistrationOptions GetRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DocumentSymbolRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(
                    new TextDocumentFilter
                    {
                        Language = "sereinscript",
                        Scheme = "file"
                    }
                ),
                Label = "SereinScript Document Symbols"
            };
        }

        /// <summary>
        /// 处理文档符号请求
        /// </summary>
        public async Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken cancellationToken) {
            Logger.Info($"Handling document symbol request for: {request.TextDocument.Uri}", "DocumentSymbol");
            var uri = request.TextDocument.Uri.ToString();
            await GetDocumentSymbolsAsync(uri);
            return new SymbolInformationOrDocumentSymbolContainer();
        }

        /// <summary>
        /// 提取文档符号
        /// </summary>
        public async Task<List<DocumentSymbol>> GetDocumentSymbolsAsync(string uri)
        {
            Logger.Info($"Getting document symbols for: {uri}", "DocumentSymbol");
            var symbols = new List<DocumentSymbol>();
            var content = _documentManager.GetDocumentContent(uri);

            if (string.IsNullOrEmpty(content))
            {
                Logger.Debug("Empty document content, returning empty symbols", "DocumentSymbol");
                return symbols;
            }

            // 使用 SyntaxAnalyzer 进行语法分析，获取 AST
            var analysisResult = _syntaxAnalyzer.PerformSyntaxAnalysis(content, uri);
            if (analysisResult.Success && analysisResult.Ast != null)
            {
                Logger.Debug("Extracting symbols from AST", "DocumentSymbol");
                ExtractSymbolsFromAst(analysisResult.Ast, symbols);
                Logger.Info($"Extracted {symbols.Count} symbols from AST", "DocumentSymbol");
            }
            else
            {
                // 如果语法分析失败，使用基于正则表达式的方法提取符号
                Logger.Warning("Syntax analysis failed, using regex-based symbol extraction", "DocumentSymbol");
                ExtractVariableSymbols(content, symbols);
                ExtractFunctionSymbols(content, symbols);
                Logger.Info($"Extracted {symbols.Count} symbols using regex", "DocumentSymbol");
            }

            return symbols;
        }

        /// <summary>
        /// 从 AST 中提取符号
        /// </summary>
        private void ExtractSymbolsFromAst(object ast, List<DocumentSymbol> symbols)
        {
            if (ast is ScriptLang.Parser.Program program)
            {
                foreach (var statement in program.Statements)
                {
                    ExtractSymbolFromExpr(statement, symbols);
                }
            }
            else if (ast is Expr expr)
            {
                ExtractSymbolFromExpr(expr, symbols);
            }
        }

        /// <summary>
        /// 从表达式中提取符号
        /// </summary>
        private void ExtractSymbolFromExpr(Expr expr, List<DocumentSymbol> symbols)
        {
            switch (expr)
            {
                case LetExpr letExpr:
                    // 检查是否是函数声明（lambda 表达式）
                    if (letExpr.Value is LambdaExpr lambdaExpr)
                    {
                        // 提取函数符号
                        symbols.Add(new DocumentSymbol
                        {
                            Name = letExpr.Name,
                            Kind = SymbolKind.Function,
                            Range = CreateRangeFromSourceSpan(letExpr.SourceSpan),
                            SelectionRange = CreateSelectionRangeFromIdentifier(letExpr.SourceSpan, letExpr.Name),
                            Detail = $"({string.Join(", ", lambdaExpr.Params)}) =>",
                            Children = ExtractSymbolsFromFunctionBody(lambdaExpr.Body)
                        });
                    }
                    else
                    {
                        // 提取变量声明符号
                        symbols.Add(new DocumentSymbol
                        {
                            Name = letExpr.Name,
                            Kind = SymbolKind.Variable,
                            Range = CreateRangeFromSourceSpan(letExpr.SourceSpan),
                            SelectionRange = CreateSelectionRangeFromIdentifier(letExpr.SourceSpan, letExpr.Name),
                            Detail = "let 声明"
                        });
                    }
                    // 递归处理值表达式
                    ExtractSymbolFromExpr(letExpr.Value, symbols);
                    break;

                case VarExpr varExpr:
                    // 检查是否是函数声明（lambda 表达式）
                    if (varExpr.Value is LambdaExpr varLambdaExpr)
                    {
                        // 提取函数符号
                        symbols.Add(new DocumentSymbol
                        {
                            Name = varExpr.Name,
                            Kind = SymbolKind.Function,
                            Range = CreateRangeFromSourceSpan(varExpr.SourceSpan),
                            SelectionRange = CreateSelectionRangeFromIdentifier(varExpr.SourceSpan, varExpr.Name),
                            Detail = $"({string.Join(", ", varLambdaExpr.Params)}) =>",
                            Children = ExtractSymbolsFromFunctionBody(varLambdaExpr.Body)
                        });
                    }
                    else
                    {
                        // 提取变量声明符号
                        symbols.Add(new DocumentSymbol
                        {
                            Name = varExpr.Name,
                            Kind = SymbolKind.Variable,
                            Range = CreateRangeFromSourceSpan(varExpr.SourceSpan),
                            SelectionRange = CreateSelectionRangeFromIdentifier(varExpr.SourceSpan, varExpr.Name),
                            Detail = "var 声明"
                        });
                    }
                    // 递归处理值表达式
                    ExtractSymbolFromExpr(varExpr.Value, symbols);
                    break;

                case BlockExpr blockExpr:
                    // 提取代码块中的符号
                    var blockSymbols = new List<DocumentSymbol>();
                    foreach (var statement in blockExpr.Statements)
                    {
                        ExtractSymbolFromExpr(statement, blockSymbols);
                    }
                    // 如果代码块有子符号，添加代码块符号
                    if (blockSymbols.Count > 0)
                    {
                        symbols.Add(new DocumentSymbol
                        {
                            Name = "代码块",
                            Kind = SymbolKind.Namespace,
                            Range = CreateRangeFromSourceSpan(blockExpr.SourceSpan),
                            SelectionRange = CreateRangeFromSourceSpan(blockExpr.SourceSpan),
                            Children = blockSymbols
                        });
                    }
                    break;

                case IfExpr ifExpr:
                    // 提取 if 语句符号
                    var ifSymbols = new List<DocumentSymbol>();
                    ExtractSymbolFromExpr(ifExpr.Cond, ifSymbols);
                    ExtractSymbolFromExpr(ifExpr.Then, ifSymbols);
                    ExtractSymbolFromExpr(ifExpr.Else, ifSymbols);
                    
                    symbols.Add(new DocumentSymbol
                    {
                        Name = "if 语句",
                        Kind = SymbolKind.Method,
                        Range = CreateRangeFromSourceSpan(ifExpr.SourceSpan),
                        SelectionRange = CreateRangeFromSourceSpan(ifExpr.SourceSpan),
                        Children = ifSymbols
                    });
                    break;

                case ForExpr forExpr:
                    // 提取 for 循环符号
                    var forSymbols = new List<DocumentSymbol>();
                    
                    // 提取循环变量
                    forSymbols.Add(new DocumentSymbol
                    {
                        Name = forExpr.VarName,
                        Kind = SymbolKind.Variable,
                        Range = CreateRangeFromSourceSpan(forExpr.SourceSpan),
                        SelectionRange = CreateSelectionRangeFromIdentifier(forExpr.SourceSpan, forExpr.VarName),
                        Detail = "循环变量"
                    });
                    
                    // 递归处理迭代器和循环体
                    ExtractSymbolFromExpr(forExpr.Iterable, forSymbols);
                    ExtractSymbolFromExpr(forExpr.Body, forSymbols);
                    
                    symbols.Add(new DocumentSymbol
                    {
                        Name = "for 循环",
                        Kind = SymbolKind.Method,
                        Range = CreateRangeFromSourceSpan(forExpr.SourceSpan),
                        SelectionRange = CreateRangeFromSourceSpan(forExpr.SourceSpan),
                        Children = forSymbols
                    });
                    break;

                case ObjectLiteralExpr objectExpr:
                    // 提取对象字面量符号
                    var objectSymbols = new List<DocumentSymbol>();
                    
                    // 提取对象属性
                    foreach (var property in objectExpr.Properties)
                    {
                        objectSymbols.Add(new DocumentSymbol
                        {
                            Name = property.Key,
                            Kind = SymbolKind.Property,
                            Range = CreateRangeFromSourceSpan(property.SourceSpan),
                            SelectionRange = CreateSelectionRangeFromIdentifier(property.SourceSpan, property.Key)
                        });
                        // 递归处理属性值
                        ExtractSymbolFromExpr(property.Value, objectSymbols);
                    }
                    
                    symbols.Add(new DocumentSymbol
                    {
                        Name = "对象字面量",
                        Kind = SymbolKind.Object,
                        Range = CreateRangeFromSourceSpan(objectExpr.SourceSpan),
                        SelectionRange = CreateRangeFromSourceSpan(objectExpr.SourceSpan),
                        Children = objectSymbols
                    });
                    break;

                case ArrayLiteralExpr arrayExpr:
                    // 提取数组字面量符号
                    var arraySymbols = new List<DocumentSymbol>();
                    
                    // 递归处理数组元素
                    foreach (var element in arrayExpr.Elements)
                    {
                        ExtractSymbolFromExpr(element, arraySymbols);
                    }
                    
                    symbols.Add(new DocumentSymbol
                    {
                        Name = "数组字面量",
                        Kind = SymbolKind.Array,
                        Range = CreateRangeFromSourceSpan(arrayExpr.SourceSpan),
                        SelectionRange = CreateRangeFromSourceSpan(arrayExpr.SourceSpan),
                        Children = arraySymbols
                    });
                    break;

                case ImportStmt importStmt:
                    // 提取导入语句符号
                    var importSymbols = new List<DocumentSymbol>();
                    
                    // 提取导入的成员
                    foreach (var member in importStmt.Members)
                    {
                        importSymbols.Add(new DocumentSymbol
                        {
                            Name = member,
                            Kind = SymbolKind.Variable,
                            Range = CreateRangeFromSourceSpan(importStmt.SourceSpan),
                            SelectionRange = CreateRangeFromSourceSpan(importStmt.SourceSpan),
                            Detail = "导入的成员"
                        });
                    }
                    
                    symbols.Add(new DocumentSymbol
                    {
                        Name = "import 语句",
                        Kind = SymbolKind.Module,
                        Range = CreateRangeFromSourceSpan(importStmt.SourceSpan),
                        SelectionRange = CreateRangeFromSourceSpan(importStmt.SourceSpan),
                        Detail = $"from \"{importStmt.FilePath}\"",
                        Children = importSymbols
                    });
                    break;

                case WhenExpr whenExpr:
                    // 提取 when 表达式符号
                    var whenSymbols = new List<DocumentSymbol>();
                    ExtractSymbolFromExpr(whenExpr.Value, whenSymbols);
                    foreach (var clause in whenExpr.Clauses)
                    {
                        ExtractSymbolFromExpr(clause.Pattern, whenSymbols);
                        ExtractSymbolFromExpr(clause.Body, whenSymbols);
                    }
                    
                    symbols.Add(new DocumentSymbol
                    {
                        Name = "when 表达式",
                        Kind = SymbolKind.Method,
                        Range = CreateRangeFromSourceSpan(whenExpr.SourceSpan),
                        SelectionRange = CreateRangeFromSourceSpan(whenExpr.SourceSpan),
                        Children = whenSymbols
                    });
                    break;

                // 其他表达式类型的处理
                case BinaryExpr binaryExpr:
                    ExtractSymbolFromExpr(binaryExpr.Left, symbols);
                    ExtractSymbolFromExpr(binaryExpr.Right, symbols);
                    break;

                case CallExpr callExpr:
                    ExtractSymbolFromExpr(callExpr.Target, symbols);
                    foreach (var arg in callExpr.Args)
                    {
                        ExtractSymbolFromExpr(arg, symbols);
                    }
                    break;

                case MemberAccessExpr memberExpr:
                    ExtractSymbolFromExpr(memberExpr.Target, symbols);
                    break;

                case IndexAccessExpr indexExpr:
                    ExtractSymbolFromExpr(indexExpr.Target, symbols);
                    ExtractSymbolFromExpr(indexExpr.Index, symbols);
                    break;

                case ConditionalExpr conditionalExpr:
                    ExtractSymbolFromExpr(conditionalExpr.Cond, symbols);
                    ExtractSymbolFromExpr(conditionalExpr.Then, symbols);
                    ExtractSymbolFromExpr(conditionalExpr.Else, symbols);
                    break;

                case UnaryExpr unaryExpr:
                    ExtractSymbolFromExpr(unaryExpr.Expr, symbols);
                    break;

                case ReturnExpr returnExpr:
                    if (returnExpr.Value != null)
                    {
                        ExtractSymbolFromExpr(returnExpr.Value, symbols);
                    }
                    break;

                case LiteralExpr:
                case IdentifierExpr:
                    break;

                case LambdaExpr lambdaExprObj:
                    // 提取lambda表达式参数作为符号
                    var lambdaSymbols = new List<DocumentSymbol>();
                    foreach (var param in lambdaExprObj.Params)
                    {
                        lambdaSymbols.Add(new DocumentSymbol
                        {
                            Name = param,
                            Kind = SymbolKind.Variable,
                            Range = CreateRangeFromSourceSpan(lambdaExprObj.SourceSpan),
                            SelectionRange = CreateRangeFromSourceSpan(lambdaExprObj.SourceSpan),
                            Detail = "lambda 参数"
                        });
                    }
                    // 提取lambda表达式体中的符号
                    lambdaSymbols.AddRange(ExtractSymbolsFromFunctionBody(lambdaExprObj.Body));
                    
                    // 添加lambda表达式符号
                    symbols.Add(new DocumentSymbol
                    {
                        Name = "lambda 表达式",
                        Kind = SymbolKind.Function,
                        Range = CreateRangeFromSourceSpan(lambdaExprObj.SourceSpan),
                        SelectionRange = CreateRangeFromSourceSpan(lambdaExprObj.SourceSpan),
                        Detail = $"({string.Join(", ", lambdaExprObj.Params)}) =>",
                        Children = lambdaSymbols
                    });
                    break;

                case AssignExpr assignExpr:
                    // 提取赋值目标作为符号
                    symbols.Add(new DocumentSymbol
                    {
                        Name = assignExpr.Name,
                        Kind = SymbolKind.Variable,
                        Range = CreateRangeFromSourceSpan(assignExpr.SourceSpan),
                        SelectionRange = CreateSelectionRangeFromIdentifier(assignExpr.SourceSpan, assignExpr.Name),
                        Detail = "赋值表达式"
                    });
                    // 递归处理值表达式
                    ExtractSymbolFromExpr(assignExpr.Value, symbols);
                    break;

                case MemberAssignExpr memberAssignExpr:
                    // 递归处理目标和值表达式
                    ExtractSymbolFromExpr(memberAssignExpr.Target, symbols);
                    ExtractSymbolFromExpr(memberAssignExpr.Value, symbols);
                    break;

                case IndexAssignExpr indexAssignExpr:
                    // 递归处理目标、索引和值表达式
                    ExtractSymbolFromExpr(indexAssignExpr.Target, symbols);
                    ExtractSymbolFromExpr(indexAssignExpr.Index, symbols);
                    ExtractSymbolFromExpr(indexAssignExpr.Value, symbols);
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// 从函数体中提取符号
        /// </summary>
        private List<DocumentSymbol> ExtractSymbolsFromFunctionBody(Expr body)
        {
            var symbols = new List<DocumentSymbol>();
            ExtractSymbolFromExpr(body, symbols);
            return symbols;
        }

        /// <summary>
        /// 从 SourceSpan 创建 Range
        /// </summary>
        private LspRange CreateRangeFromSourceSpan(SourceSpan sourceSpan)
        {
            return new LspRange
            {
                Start = new LspPosition { Line = sourceSpan.StartLine - 1, Character = sourceSpan.StartColumn - 1 },
                End = new LspPosition { Line = sourceSpan.EndLine - 1, Character = sourceSpan.EndColumn - 1 }
            };
        }

        /// <summary>
        /// 从标识符创建选择范围
        /// </summary>
        private LspRange CreateSelectionRangeFromIdentifier(SourceSpan sourceSpan, string identifier)
        {
            // 假设标识符在 SourceSpan 的开始位置
            return new LspRange
            {
                Start = new LspPosition { Line = sourceSpan.StartLine - 1, Character = sourceSpan.StartColumn - 1 },
                End = new LspPosition { Line = sourceSpan.StartLine - 1, Character = sourceSpan.StartColumn + identifier.Length - 2 }
            };
        }

        /// <summary>
        /// 提取变量声明符号
        /// </summary>
        private void ExtractVariableSymbols(string content, List<DocumentSymbol> symbols)
        {
            // 匹配变量声明，如: let a = 10
            var variablePattern = new Regex(@"^\s*let\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*=", RegexOptions.Multiline);
            var matches = variablePattern.Matches(content);

            foreach (Match match in matches)
            {
                var variableName = match.Groups[1].Value;
                var line = GetLineNumber(content, match.Index);
                var column = GetColumnNumber(content, match.Index, line);

                symbols.Add(new DocumentSymbol
                {
                    Name = variableName,
                    Kind = SymbolKind.Variable,
                    Range = new LspRange
                    {
                        Start = new LspPosition { Line = line - 1, Character = column - 1 },
                        End = new LspPosition { Line = line - 1, Character = column + variableName.Length - 1 }
                    },
                    SelectionRange = new LspRange
                    {
                        Start = new LspPosition { Line = line - 1, Character = column - 1 },
                        End = new LspPosition { Line = line - 1, Character = column + variableName.Length - 1 }
                    }
                });
            }
        }

        /// <summary>
        /// 提取函数定义符号
        /// </summary>
        private void ExtractFunctionSymbols(string content, List<DocumentSymbol> symbols)
        {
            // 匹配函数定义，如: let add = (a, b) => a + b
            var functionPattern = new Regex(@"^\s*let\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*\(([^)]*)\)\s*=>", RegexOptions.Multiline);
            var matches = functionPattern.Matches(content);

            foreach (Match match in matches)
            {
                var functionName = match.Groups[1].Value;
                var parameters = match.Groups[2].Value;
                var line = GetLineNumber(content, match.Index);
                var column = GetColumnNumber(content, match.Index, line);

                symbols.Add(new DocumentSymbol
                {
                    Name = functionName,
                    Kind = SymbolKind.Function,
                    Range = new LspRange
                    {
                        Start = new LspPosition { Line = line - 1, Character = column - 1 },
                        End = new LspPosition { Line = line - 1, Character = column + functionName.Length - 1 }
                    },
                    SelectionRange = new LspRange
                    {
                        Start = new LspPosition { Line = line - 1, Character = column - 1 },
                        End = new LspPosition { Line = line - 1, Character = column + functionName.Length - 1 }
                    },
                    Detail = $"({parameters}) =>"
                });
            }
        }

        /// <summary>
        /// 获取行号
        /// </summary>
        private int GetLineNumber(string content, int index)
        {
            return content.Substring(0, index).Split('\n').Length;
        }

        /// <summary>
        /// 获取列号
        /// </summary>
        private int GetColumnNumber(string content, int index, int line)
        {
            var lines = content.Substring(0, index).Split('\n');
            return lines[lines.Length - 1].Length + 1;
        }

    }
}
