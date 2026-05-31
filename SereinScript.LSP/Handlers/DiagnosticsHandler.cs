using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using ScriptLang.Parser;
using SereinScript.LSP;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace SereinScript.LSP.Handlers
{
    /// <summary>
    /// 语法检查和诊断处理器
    /// </summary>
    public class DiagnosticsHandler 
    {
        private readonly DocumentManager _documentManager;
        private readonly SamplesParser _samplesParser;
        private readonly SyntaxAnalyzer _syntaxAnalyzer;
        private readonly ILanguageServerFacade _server;

        public DiagnosticsHandler(DocumentManager documentManager, SamplesParser samplesParser, SyntaxAnalyzer syntaxAnalyzer, ILanguageServerFacade server)
        {
            _documentManager = documentManager;
            _samplesParser = samplesParser;
            _syntaxAnalyzer = syntaxAnalyzer;
            _server = server;
        }

        /// <summary>
        /// 检查文档语法并发布诊断信息
        /// </summary>
        /// <param name="uri">文档 URI</param>
        public async Task PublishDiagnosticsAsync(string uri)
        {
            Logger.Info($"Publishing diagnostics for: {uri}", "Diagnostics");
            try
            {
                var diagnostics = new List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic>();
                var content = _documentManager.GetDocumentContent(uri);

                if (string.IsNullOrEmpty(content))
                {
                    Logger.Debug("Empty document content, sending empty diagnostics", "Diagnostics");
                    _server.SendNotification(new PublishDiagnosticsParams
                    {
                        Uri = uri,
                        Diagnostics = diagnostics
                    });
                    return;
                }

                // 使用 SyntaxAnalyzer 进行精确的语法检查
                Logger.Debug("Performing syntax analysis", "Diagnostics");
                var syntaxDiagnostics = _syntaxAnalyzer.AnalyzeAndGetDiagnostics(content, uri);
                diagnostics.AddRange(syntaxDiagnostics);
                Logger.Debug($"Found {syntaxDiagnostics.Count} syntax diagnostics", "Diagnostics");

                // 执行语法分析以获取 AST
                var analysisResult = _syntaxAnalyzer.PerformSyntaxAnalysis(content, uri);
                if (analysisResult.Success && analysisResult.Ast != null)
                {
                    // 基于 AST 添加更详细的诊断信息
                    Logger.Debug("Adding AST-based diagnostics", "Diagnostics");
                    AddAstBasedDiagnostics(analysisResult.Ast, content, uri, diagnostics);
                }
                else
                {
                    Logger.Warning($"Syntax analysis failed: {analysisResult.ErrorMessage}", "Diagnostics");
                }

                // 添加代码质量检查
                Logger.Debug("Adding code quality diagnostics", "Diagnostics");
                AddCodeQualityDiagnostics(content, uri, diagnostics);

                // 发布诊断信息
                Logger.Info($"Sending {diagnostics.Count} diagnostics", "Diagnostics");
                _server.SendNotification(new PublishDiagnosticsParams
                {
                    Uri = uri,
                    Diagnostics = diagnostics
                });
                Logger.Info("Diagnostics published successfully", "Diagnostics");
            }
            catch (Exception ex)
            {
                // 记录详细错误
                Logger.Error($"Error publishing diagnostics: {ex.Message}", "Diagnostics");
                Logger.Error($"Error stack trace: {ex.StackTrace}", "Diagnostics");
            }
        }

        /// <summary>
        /// 基于 AST 添加详细诊断信息
        /// </summary>
        /// <param name="ast">抽象语法树</param>
        /// <param name="content">文档内容</param>
        /// <param name="uri">文档 URI</param>
        /// <param name="diagnostics">诊断信息列表</param>
        private void AddAstBasedDiagnostics(Expr ast, string content, string uri, List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic> diagnostics)
        {
            // 遍历 AST 并添加特定的诊断信息
            VisitAstNode(ast, content, uri, diagnostics);
        }

        /// <summary>
        /// 访问 AST 节点并添加诊断信息
        /// </summary>
        private void VisitAstNode(Expr node, string content, string uri, List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic> diagnostics)
        {
            switch (node)
            {
                case ScriptLang.Parser.Program program:
                    foreach (var statement in program.Statements)
                    {
                        VisitAstNode(statement, content, uri, diagnostics);
                    }
                    break;

                case LetExpr letExpr:
                    // 检查变量名长度
                    if (letExpr.Name.Length > 30)
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = "变量名过长，建议使用更简洁的名称",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Hint,
                            Range = CreateRangeFromSourceSpan(letExpr.SourceSpan)
                        });
                    }
                    VisitAstNode(letExpr.Value, content, uri, diagnostics);
                    break;

                case VarExpr varExpr:
                    // 检查变量名长度
                    if (varExpr.Name.Length > 30)
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = "变量名过长，建议使用更简洁的名称",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Hint,
                            Range = CreateRangeFromSourceSpan(varExpr.SourceSpan)
                        });
                    }
                    VisitAstNode(varExpr.Value, content, uri, diagnostics);
                    break;

                case BinaryExpr binaryExpr:
                    VisitAstNode(binaryExpr.Left, content, uri, diagnostics);
                    VisitAstNode(binaryExpr.Right, content, uri, diagnostics);
                    break;

                case CallExpr callExpr:
                    // 检查函数调用参数数量
                    if (callExpr.Args.Count > 10)
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = "函数调用参数过多，建议使用对象参数",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Warning,
                            Range = CreateRangeFromSourceSpan(callExpr.SourceSpan)
                        });
                    }
                    VisitAstNode(callExpr.Target, content, uri, diagnostics);
                    foreach (var arg in callExpr.Args)
                    {
                        VisitAstNode(arg, content, uri, diagnostics);
                    }
                    break;

                case IfExpr ifExpr:
                    VisitAstNode(ifExpr.Cond, content, uri, diagnostics);
                    VisitAstNode(ifExpr.Then, content, uri, diagnostics);
                    VisitAstNode(ifExpr.Else, content, uri, diagnostics);
                    break;

                case ForExpr forExpr:
                    VisitAstNode(forExpr.Iterable, content, uri, diagnostics);
                    VisitAstNode(forExpr.Body, content, uri, diagnostics);
                    break;

                case BlockExpr blockExpr:
                    // 检查代码块大小
                    if (blockExpr.Statements.Count > 50)
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = "代码块过大，建议拆分为多个函数",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Warning,
                            Range = CreateRangeFromSourceSpan(blockExpr.SourceSpan)
                        });
                    }
                    foreach (var statement in blockExpr.Statements)
                    {
                        VisitAstNode(statement, content, uri, diagnostics);
                    }
                    break;

                case ObjectLiteralExpr objectExpr:
                    foreach (var property in objectExpr.Properties)
                    {
                        VisitAstNode(property.Value, content, uri, diagnostics);
                    }
                    break;

                case ArrayLiteralExpr arrayExpr:
                    foreach (var element in arrayExpr.Elements)
                    {
                        VisitAstNode(element, content, uri, diagnostics);
                    }
                    break;

                case ImportStmt importStmt:
                    // 检查导入成员数量
                    if (importStmt.Members.Count > 10)
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = "导入成员过多，建议只导入需要的成员",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Warning,
                            Range = CreateRangeFromSourceSpan(importStmt.SourceSpan)
                        });
                    }
                    break;

                case MemberAccessExpr memberAccessExpr:
                    VisitAstNode(memberAccessExpr.Target, content, uri, diagnostics);
                    break;

                case IndexAccessExpr indexAccessExpr:
                    VisitAstNode(indexAccessExpr.Target, content, uri, diagnostics);
                    VisitAstNode(indexAccessExpr.Index, content, uri, diagnostics);
                    break;

                case IndexAssignExpr indexAssignExpr:
                    VisitAstNode(indexAssignExpr.Target, content, uri, diagnostics);
                    VisitAstNode(indexAssignExpr.Index, content, uri, diagnostics);
                    VisitAstNode(indexAssignExpr.Value, content, uri, diagnostics);
                    break;

                case MemberAssignExpr memberAssignExpr:
                    VisitAstNode(memberAssignExpr.Target, content, uri, diagnostics);
                    VisitAstNode(memberAssignExpr.Value, content, uri, diagnostics);
                    break;

                case WhenExpr whenExpr:
                    VisitAstNode(whenExpr.Value, content, uri, diagnostics);
                    foreach (var clause in whenExpr.Clauses)
                    {
                        VisitAstNode(clause.Pattern, content, uri, diagnostics);
                        VisitAstNode(clause.Body, content, uri, diagnostics);
                    }
                    break;

                case UnaryExpr unaryExpr:
                    VisitAstNode(unaryExpr.Expr, content, uri, diagnostics);
                    break;

                case ConditionalExpr conditionalExpr:
                    VisitAstNode(conditionalExpr.Cond, content, uri, diagnostics);
                    VisitAstNode(conditionalExpr.Then, content, uri, diagnostics);
                    VisitAstNode(conditionalExpr.Else, content, uri, diagnostics);
                    break;

                case LambdaExpr lambdaExpr:
                    // 检查lambda表达式参数数量
                    if (lambdaExpr.Params.Count > 5)
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = "Lambda表达式参数过多，建议使用对象参数",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Warning,
                            Range = CreateRangeFromSourceSpan(lambdaExpr.SourceSpan)
                        });
                    }
                    VisitAstNode(lambdaExpr.Body, content, uri, diagnostics);
                    break;

                case ReturnExpr returnExpr:
                    if (returnExpr.Value != null)
                    {
                        VisitAstNode(returnExpr.Value, content, uri, diagnostics);
                    }
                    break;

                case AssignExpr assignExpr:
                    VisitAstNode(assignExpr.Value, content, uri, diagnostics);
                    break;

                // 其他 AST 节点的处理
                default:
                    break;
            }
        }

        /// <summary>
        /// 添加代码质量诊断信息
        /// </summary>
        /// <param name="content">文档内容</param>
        /// <param name="uri">文档 URI</param>
        /// <param name="diagnostics">诊断信息列表</param>
        private void AddCodeQualityDiagnostics(string content, string uri, List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic> diagnostics)
        {
            // 检查空的代码块
            var emptyBlockPattern = new Regex(@"\{\s*\}", RegexOptions.Multiline);
            var emptyBlockMatches = emptyBlockPattern.Matches(content);
            foreach (Match match in emptyBlockMatches)
            {
                var line = GetLineNumber(content, match.Index);
                var column = GetColumnNumber(content, match.Index, line);
                
                diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                {
                    Message = "空的代码块",
                    Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Warning,
                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                    {
                        Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = line - 1, Character = column - 1 },
                        End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = line - 1, Character = column + match.Length - 1 }
                    }
                });
            }

            // 检查多余的空白行
            var extraEmptyLinesPattern = new Regex(@"\n\s*\n\s*\n", RegexOptions.Multiline);
            var extraEmptyLinesMatches = extraEmptyLinesPattern.Matches(content);
            foreach (Match match in extraEmptyLinesMatches)
            {
                var line = GetLineNumber(content, match.Index);
                var column = GetColumnNumber(content, match.Index, line);
                
                diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                {
                    Message = "多余的空白行",
                    Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Hint,
                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                    {
                        Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = line - 1, Character = column - 1 },
                        End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = line + 1, Character = 0 }
                    }
                });
            }

            // 检查行尾空白
            var trailingWhitespacePattern = new Regex(@"\s+$", RegexOptions.Multiline);
            var trailingWhitespaceMatches = trailingWhitespacePattern.Matches(content);
            foreach (Match match in trailingWhitespaceMatches)
            {
                var line = GetLineNumber(content, match.Index);
                var column = GetColumnNumber(content, match.Index, line);
                
                diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                {
                    Message = "行尾有多余的空白",
                    Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Hint,
                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                    {
                        Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = line - 1, Character = column - 1 },
                        End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = line - 1, Character = column + match.Length - 1 }
                    }
                });
            }

            // 检查过长的行
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length > 120)
                {
                    diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                    {
                        Message = "行过长，建议不超过120个字符",
                        Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Hint,
                        Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                        {
                            Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = i, Character = 0 },
                            End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = i, Character = line.Length }
                        }
                    });
                }
            }

            // 检查未使用的导入
            var importPattern = new Regex(@"import\s*\{([^}]*)\}\s*from\s*""([^""]*)""", RegexOptions.Multiline);
            var importMatches = importPattern.Matches(content);
            foreach (Match match in importMatches)
            {
                var members = match.Groups[1].Value.Split(',').Select(m => m.Trim()).Where(m => !string.IsNullOrEmpty(m)).ToList();
                var filePath = match.Groups[2].Value;
                var line = GetLineNumber(content, match.Index);
                var column = GetColumnNumber(content, match.Index, line);

                foreach (var member in members)
                {
                    // 检查成员是否在代码中使用
                    var memberPattern = new Regex($@"\b{Regex.Escape(member)}\b", RegexOptions.Multiline);
                    var memberMatches = memberPattern.Matches(content);
                    var isUsed = false;

                    foreach (Match memberMatch in memberMatches)
                    {
                        // 确保匹配不是在导入语句中
                        if (memberMatch.Index < match.Index || memberMatch.Index > match.Index + match.Length)
                        {
                            isUsed = true;
                            break;
                        }
                    }

                    if (!isUsed)
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = $"导入的成员 '{member}' 未使用",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Warning,
                            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                            {
                                Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = line - 1, Character = column + match.Groups[1].Index - match.Index },
                                End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = line - 1, Character = column + match.Groups[1].Index - match.Index + match.Groups[1].Length }
                            }
                        });
                    }
                }
            }

            // 检查重复的变量声明
            var varPattern = new Regex(@"\b(let|var)\s+([a-zA-Z_]\w*)\s*=", RegexOptions.Multiline);
            var varMatches = varPattern.Matches(content);
            var declaredVars = new Dictionary<string, int>();

            foreach (Match match in varMatches)
            {
                var varName = match.Groups[2].Value;
                var line = GetLineNumber(content, match.Index);

                if (declaredVars.ContainsKey(varName))
                {
                    diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                    {
                        Message = $"变量 '{varName}' 已在第 {declaredVars[varName]} 行声明",
                        Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Error,
                        Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                        {
                            Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = line - 1, Character = match.Groups[2].Index - match.Index },
                            End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = line - 1, Character = match.Groups[2].Index - match.Index + match.Groups[2].Length }
                        }
                    });
                }
                else
                {
                    declaredVars[varName] = line;
                }
            }
        }

        /// <summary>
        /// 从 SourceSpan 创建 Range
        /// </summary>
        private OmniSharp.Extensions.LanguageServer.Protocol.Models.Range CreateRangeFromSourceSpan(SourceSpan sourceSpan)
        {
            return new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
            {
                Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position
                {
                    Line = sourceSpan.StartLine - 1,
                    Character = sourceSpan.StartColumn - 1
                },
                End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position
                {
                    Line = sourceSpan.EndLine - 1,
                    Character = sourceSpan.EndColumn
                }
            };
        }

        /// <summary>
        /// 获取行号
        /// </summary>
        /// <param name="content">文档内容</param>
        /// <param name="index">索引</param>
        /// <returns>行号</returns>
        private int GetLineNumber(string content, int index)
        {
            return content.Substring(0, index).Split('\n').Length;
        }

        /// <summary>
        /// 获取列号
        /// </summary>
        /// <param name="content">文档内容</param>
        /// <param name="index">索引</param>
        /// <param name="line">行号</param>
        /// <returns>列号</returns>
        private int GetColumnNumber(string content, int index, int line)
        {
            var lines = content.Substring(0, index).Split('\n');
            return lines[lines.Length - 1].Length + 1;
        }

    }
}
