using ScriptLang.Lexer;
using ScriptLang.Parser;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SereinScript.LSP;

namespace SereinScript.LSP
{
    /// <summary>
    /// 语法分析服务，封装 ScriptLang 的 Lexer 和 Parser 功能
    /// </summary>
    public class SyntaxAnalyzer
    {
        /// <summary>
        /// 执行词法分析
        /// </summary>
        /// <param name="source">源代码</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>Token 列表</returns>
        public List<Token> PerformLexicalAnalysis(string source, string filePath)
        {
            Logger.Info($"Performing lexical analysis for: {filePath}", "SyntaxAnalyzer");
            try
            {
                var lexer = new Lexer(source, filePath);
                var tokens = lexer.ScanTokens();
                Logger.Info($"Lexical analysis completed, found {tokens.Count} tokens", "SyntaxAnalyzer");
                return tokens;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in lexical analysis: {ex.Message}", "SyntaxAnalyzer");
                return new List<Token>();
            }
        }

        /// <summary>
        /// 执行语法分析
        /// </summary>
        /// <param name="source">源代码</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>语法分析结果，包含 AST 和错误信息</returns>
        public SyntaxAnalysisResult PerformSyntaxAnalysis(string source, string filePath)
        {
            Logger.Info($"Performing syntax analysis for: {filePath}", "SyntaxAnalyzer");
            var result = new SyntaxAnalysisResult();

            try
            {
                // 执行词法分析
                var lexer = new Lexer(source, filePath);
                var tokens = lexer.ScanTokens();
                result.Tokens = tokens;
                Logger.Debug($"Lexical analysis completed, found {tokens.Count} tokens", "SyntaxAnalyzer");

                // 执行语法分析
                var parser = new Parser(tokens, filePath);
                var ast = parser.Parse();
                result.Ast = ast;
                result.Success = true;
                Logger.Info("Syntax analysis completed successfully", "SyntaxAnalyzer");
            }
            catch (ParseException ex)
            {
                // 捕获解析错误
                result.Success = false;
                result.Error = ex;
                result.ErrorMessage = ex.Message;
                result.ErrorLine = ex.Line;
                result.ErrorColumn = ex.Column;
                Logger.Error($"Parse error: {ex.Message} at line {ex.Line}, column {ex.Column}", "SyntaxAnalyzer");
            }
            catch (Exception ex)
            {
                // 捕获其他错误
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Logger.Error($"Error in syntax analysis: {ex.Message}", "SyntaxAnalyzer");
            }

            return result;
        }

        /// <summary>
        /// 分析文档并生成诊断信息
        /// </summary>
        /// <param name="source">源代码</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>诊断信息列表</returns>
        public List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic> AnalyzeAndGetDiagnostics(string source, string filePath)
        {
            Logger.Info($"Analyzing and getting diagnostics for: {filePath}", "SyntaxAnalyzer");
            var diagnostics = new List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic>();
            var analysisResult = PerformSyntaxAnalysis(source, filePath);

            if (!analysisResult.Success && !string.IsNullOrEmpty(analysisResult.ErrorMessage))
            {
                // 添加语法错误诊断
                diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                {
                    Message = analysisResult.ErrorMessage,
                    Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Error,
                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                    {
                        Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = analysisResult.ErrorLine - 1, Character = analysisResult.ErrorColumn - 1 },
                        End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = analysisResult.ErrorLine - 1, Character = analysisResult.ErrorColumn }
                    }
                });
                Logger.Warning($"Added syntax error diagnostic: {analysisResult.ErrorMessage}", "SyntaxAnalyzer");
            }
            else if (analysisResult.Success && analysisResult.Ast != null)
            {
                // 基于AST进行深度分析，传递源代码
                AnalyzeAst(analysisResult.Ast, diagnostics, source);
                Logger.Info($"AST analysis completed, found {diagnostics.Count} diagnostics", "SyntaxAnalyzer");
            }

            return diagnostics;
        }

        /// <summary>
        /// 分析AST并生成诊断信息
        /// </summary>
        /// <param name="ast">AST节点</param>
        /// <param name="diagnostics">诊断信息列表</param>
        private void AnalyzeAst(Expr ast, List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic> diagnostics)
        {
            AnalyzeAst(ast, diagnostics, string.Empty);
        }

        /// <summary>
        /// 分析AST并生成诊断信息（带源代码）
        /// </summary>
        /// <param name="ast">AST节点</param>
        /// <param name="diagnostics">诊断信息列表</param>
        /// <param name="source">源代码</param>
        private void AnalyzeAst(Expr ast, List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic> diagnostics, string source)
        {
            switch (ast)
            {
                case ScriptLang.Parser.Program program:
                    var declaredVariables = new HashSet<string>();
                    var usedVariables = new HashSet<string>();
                    
                    foreach (var statement in program.Statements)
                    {
                        AnalyzeAstWithVariableTracking(statement, diagnostics, declaredVariables, usedVariables, source);
                    }
                    
                    // 检查未使用的变量
                    foreach (var variable in declaredVariables)
                    {
                        if (!usedVariables.Contains(variable))
                        {
                            // 查找变量声明位置
                            var variablePosition = FindVariableDeclarationPosition(source, variable);
                            if (variablePosition.Line > 0)
                            {
                                diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                                {
                                    Message = $"变量 '{variable}' 已声明但未使用",
                                    Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Warning,
                                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                                    {
                                        Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = variablePosition.Line - 1, Character = variablePosition.Column - 1 },
                                        End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position { Line = variablePosition.Line - 1, Character = variablePosition.Column + variable.Length - 1 }
                                    }
                                });
                            }
                        }
                    }
                    break;

                case LetExpr letExpr:
                    // 检查变量名是否为关键字
                    if (IsKeyword(letExpr.Name))
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = $"变量名 '{letExpr.Name}' 不能使用关键字",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Error,
                            Range = CreateRangeFromSourceSpan(letExpr.SourceSpan)
                        });
                    }
                    AnalyzeAst(letExpr.Value, diagnostics, source);
                    break;

                case VarExpr varExpr:
                    // 检查变量名是否为关键字
                    if (IsKeyword(varExpr.Name))
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = $"变量名 '{varExpr.Name}' 不能使用关键字",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Error,
                            Range = CreateRangeFromSourceSpan(varExpr.SourceSpan)
                        });
                    }
                    AnalyzeAst(varExpr.Value, diagnostics, source);
                    break;

                case BinaryExpr binaryExpr:
                    AnalyzeAst(binaryExpr.Left, diagnostics, source);
                    AnalyzeAst(binaryExpr.Right, diagnostics, source);
                    break;

                case CallExpr callExpr:
                    AnalyzeAst(callExpr.Target, diagnostics, source);
                    foreach (var arg in callExpr.Args)
                    {
                        AnalyzeAst(arg, diagnostics, source);
                    }
                    break;

                case LambdaExpr lambdaExpr:
                    AnalyzeAst(lambdaExpr.Body, diagnostics, source);
                    break;

                case IfExpr ifExpr:
                    AnalyzeAst(ifExpr.Cond, diagnostics, source);
                    AnalyzeAst(ifExpr.Then, diagnostics, source);
                    AnalyzeAst(ifExpr.Else, diagnostics, source);
                    break;

                case ForExpr forExpr:
                    AnalyzeAst(forExpr.Iterable, diagnostics, source);
                    AnalyzeAst(forExpr.Body, diagnostics, source);
                    break;

                case ObjectLiteralExpr objectExpr:
                    foreach (var property in objectExpr.Properties)
                    {
                        AnalyzeAst(property.Value, diagnostics, source);
                    }
                    break;

                case ArrayLiteralExpr arrayExpr:
                    foreach (var element in arrayExpr.Elements)
                    {
                        AnalyzeAst(element, diagnostics, source);
                    }
                    break;

                case ImportStmt importStmt:
                    // 检查导入路径是否有效
                    if (string.IsNullOrEmpty(importStmt.FilePath))
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = "导入路径不能为空",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Error,
                            Range = CreateRangeFromSourceSpan(importStmt.SourceSpan)
                        });
                    }
                    // 检查导入路径格式
                    else if (!importStmt.FilePath.EndsWith(".script", StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = "导入路径应该指向 .script 文件",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Warning,
                            Range = CreateRangeFromSourceSpan(importStmt.SourceSpan)
                        });
                    }
                    break;

                case AssignExpr assignExpr:
                    // 检查赋值目标是否为关键字
                    if (IsKeyword(assignExpr.Name))
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = $"不能给关键字 '{assignExpr.Name}' 赋值",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Error,
                            Range = CreateRangeFromSourceSpan(assignExpr.SourceSpan)
                        });
                    }
                    AnalyzeAst(assignExpr.Value, diagnostics, source);
                    break;

                case ReturnExpr returnExpr:
                    if (returnExpr.Value != null)
                    {
                        AnalyzeAst(returnExpr.Value, diagnostics, source);
                    }
                    break;

                case BlockExpr blockExpr:
                    foreach (var statement in blockExpr.Statements)
                    {
                        AnalyzeAst(statement, diagnostics, source);
                    }
                    break;

                case MemberAccessExpr memberAccessExpr:
                    AnalyzeAst(memberAccessExpr.Target, diagnostics, source);
                    break;

                case IndexAccessExpr indexAccessExpr:
                    AnalyzeAst(indexAccessExpr.Target, diagnostics, source);
                    AnalyzeAst(indexAccessExpr.Index, diagnostics, source);
                    break;

                case IndexAssignExpr indexAssignExpr:
                    AnalyzeAst(indexAssignExpr.Target, diagnostics, source);
                    AnalyzeAst(indexAssignExpr.Index, diagnostics, source);
                    AnalyzeAst(indexAssignExpr.Value, diagnostics, source);
                    break;

                case MemberAssignExpr memberAssignExpr:
                    AnalyzeAst(memberAssignExpr.Target, diagnostics, source);
                    AnalyzeAst(memberAssignExpr.Value, diagnostics, source);
                    break;

                case WhenExpr whenExpr:
                    AnalyzeAst(whenExpr.Value, diagnostics, source);
                    foreach (var clause in whenExpr.Clauses)
                    {
                        AnalyzeAst(clause.Pattern, diagnostics, source);
                        AnalyzeAst(clause.Body, diagnostics, source);
                    }
                    break;

                case UnaryExpr unaryExpr:
                    AnalyzeAst(unaryExpr.Expr, diagnostics, source);
                    break;

                case ConditionalExpr conditionalExpr:
                    AnalyzeAst(conditionalExpr.Cond, diagnostics, source);
                    AnalyzeAst(conditionalExpr.Then, diagnostics, source);
                    AnalyzeAst(conditionalExpr.Else, diagnostics, source);
                    break;

                // 其他AST节点类型的分析
                default:
                    break;
            }
        }

        /// <summary>
        /// 分析AST并跟踪变量使用情况
        /// </summary>
        /// <param name="ast">AST节点</param>
        /// <param name="diagnostics">诊断信息列表</param>
        /// <param name="declaredVariables">已声明变量集合</param>
        /// <param name="usedVariables">已使用变量集合</param>
        /// <param name="source">源代码</param>
        private void AnalyzeAstWithVariableTracking(Expr ast, List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic> diagnostics, HashSet<string> declaredVariables, HashSet<string> usedVariables, string source)
        {
            switch (ast)
            {
                case LetExpr letExpr:
                    declaredVariables.Add(letExpr.Name);
                    AnalyzeAstWithVariableTracking(letExpr.Value, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case VarExpr varExpr:
                    declaredVariables.Add(varExpr.Name);
                    AnalyzeAstWithVariableTracking(varExpr.Value, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case AssignExpr assignExpr:
                    usedVariables.Add(assignExpr.Name);
                    AnalyzeAstWithVariableTracking(assignExpr.Value, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case IdentifierExpr identifierExpr:
                    usedVariables.Add(identifierExpr.Name);
                    // 检查变量是否已声明
                    if (!declaredVariables.Contains(identifierExpr.Name) && !IsBuiltInFunction(identifierExpr.Name))
                    {
                        diagnostics.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                        {
                            Message = $"变量 '{identifierExpr.Name}' 未声明",
                            Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Error,
                            Range = CreateRangeFromSourceSpan(identifierExpr.SourceSpan)
                        });
                    }
                    break;

                case BinaryExpr binaryExpr:
                    AnalyzeAstWithVariableTracking(binaryExpr.Left, diagnostics, declaredVariables, usedVariables, source);
                    AnalyzeAstWithVariableTracking(binaryExpr.Right, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case CallExpr callExpr:
                    if (callExpr.Target is IdentifierExpr identifier)
                    {
                        usedVariables.Add(identifier.Name);
                    }
                    foreach (var arg in callExpr.Args)
                    {
                        AnalyzeAstWithVariableTracking(arg, diagnostics, declaredVariables, usedVariables, source);
                    }
                    break;

                case BlockExpr blockExpr:
                    foreach (var statement in blockExpr.Statements)
                    {
                        AnalyzeAstWithVariableTracking(statement, diagnostics, declaredVariables, usedVariables, source);
                    }
                    break;

                case IfExpr ifExpr:
                    AnalyzeAstWithVariableTracking(ifExpr.Cond, diagnostics, declaredVariables, usedVariables, source);
                    AnalyzeAstWithVariableTracking(ifExpr.Then, diagnostics, declaredVariables, usedVariables, source);
                    AnalyzeAstWithVariableTracking(ifExpr.Else, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case ForExpr forExpr:
                    declaredVariables.Add(forExpr.VarName);
                    AnalyzeAstWithVariableTracking(forExpr.Iterable, diagnostics, declaredVariables, usedVariables, source);
                    AnalyzeAstWithVariableTracking(forExpr.Body, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case MemberAccessExpr memberAccessExpr:
                    AnalyzeAstWithVariableTracking(memberAccessExpr.Target, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case IndexAccessExpr indexAccessExpr:
                    AnalyzeAstWithVariableTracking(indexAccessExpr.Target, diagnostics, declaredVariables, usedVariables, source);
                    AnalyzeAstWithVariableTracking(indexAccessExpr.Index, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case IndexAssignExpr indexAssignExpr:
                    AnalyzeAstWithVariableTracking(indexAssignExpr.Target, diagnostics, declaredVariables, usedVariables, source);
                    AnalyzeAstWithVariableTracking(indexAssignExpr.Index, diagnostics, declaredVariables, usedVariables, source);
                    AnalyzeAstWithVariableTracking(indexAssignExpr.Value, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case MemberAssignExpr memberAssignExpr:
                    AnalyzeAstWithVariableTracking(memberAssignExpr.Target, diagnostics, declaredVariables, usedVariables, source);
                    AnalyzeAstWithVariableTracking(memberAssignExpr.Value, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case WhenExpr whenExpr:
                    AnalyzeAstWithVariableTracking(whenExpr.Value, diagnostics, declaredVariables, usedVariables, source);
                    foreach (var clause in whenExpr.Clauses)
                    {
                        AnalyzeAstWithVariableTracking(clause.Pattern, diagnostics, declaredVariables, usedVariables, source);
                        AnalyzeAstWithVariableTracking(clause.Body, diagnostics, declaredVariables, usedVariables, source);
                    }
                    break;

                case UnaryExpr unaryExpr:
                    AnalyzeAstWithVariableTracking(unaryExpr.Expr, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case ConditionalExpr conditionalExpr:
                    AnalyzeAstWithVariableTracking(conditionalExpr.Cond, diagnostics, declaredVariables, usedVariables, source);
                    AnalyzeAstWithVariableTracking(conditionalExpr.Then, diagnostics, declaredVariables, usedVariables, source);
                    AnalyzeAstWithVariableTracking(conditionalExpr.Else, diagnostics, declaredVariables, usedVariables, source);
                    break;

                case ObjectLiteralExpr objectExpr:
                    foreach (var property in objectExpr.Properties)
                    {
                        AnalyzeAstWithVariableTracking(property.Value, diagnostics, declaredVariables, usedVariables, source);
                    }
                    break;

                case ArrayLiteralExpr arrayExpr:
                    foreach (var element in arrayExpr.Elements)
                    {
                        AnalyzeAstWithVariableTracking(element, diagnostics, declaredVariables, usedVariables, source);
                    }
                    break;

                case ReturnExpr returnExpr:
                    if (returnExpr.Value != null)
                    {
                        AnalyzeAstWithVariableTracking(returnExpr.Value, diagnostics, declaredVariables, usedVariables, source);
                    }
                    break;

                // 其他AST节点类型的分析
                default:
                    AnalyzeAst(ast, diagnostics, source);
                    break;
            }
        }

        /// <summary>
        /// 查找变量声明位置
        /// </summary>
        /// <param name="source">源代码</param>
        /// <param name="variableName">变量名</param>
        /// <returns>变量位置信息</returns>
        private VariablePosition FindVariableDeclarationPosition(string source, string variableName)
        {
            var result = new VariablePosition();
            if (string.IsNullOrEmpty(source))
                return result;

            var lines = source.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNumber = i + 1;

                // 查找 let 或 var 声明
                if (line.Contains($"let {variableName}") || line.Contains($"var {variableName}"))
                {
                    var column = line.IndexOf(variableName);
                    if (column >= 0)
                    {
                        result.Line = lineNumber;
                        result.Column = column + 1;
                        result.Length = variableName.Length;
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 检查是否为内置函数
        /// </summary>
        /// <param name="identifier">标识符</param>
        /// <returns>是否为内置函数</returns>
        private bool IsBuiltInFunction(string identifier)
        {
            var builtInFunctions = new[] {
                "print", "println", "read", "readLine", "parseInt", "parseFloat", "toString", "typeof"
            };
            return builtInFunctions.Contains(identifier);
        }

        /// <summary>
        /// 变量位置信息
        /// </summary>
        private class VariablePosition
        {
            public int Line { get; set; } = 0;
            public int Column { get; set; } = 0;
            public int Length { get; set; } = 0;
        }

        /// <summary>
        /// 检查字符串是否为关键字
        /// </summary>
        /// <param name="identifier">标识符</param>
        /// <returns>是否为关键字</returns>
        private bool IsKeyword(string identifier)
        {
            var keywords = new[] {
                "let", "var", "if", "then", "else", "when", "for", "in", "return",
                "import", "from", "true", "false", "null"
            };
            return keywords.Contains(identifier);
        }

        /// <summary>
        /// 从SourceSpan创建Range
        /// </summary>
        /// <param name="sourceSpan">SourceSpan</param>
        /// <returns>Range</returns>
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
    }

    /// <summary>
    /// 语法分析结果
    /// </summary>
    public class SyntaxAnalysisResult
    {
        /// <summary>
        /// 分析是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 词法分析生成的 Token 列表
        /// </summary>
        public List<Token>? Tokens { get; set; }

        /// <summary>
        /// 语法分析生成的 AST
        /// </summary>
        public Expr? Ast { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 错误行号
        /// </summary>
        public int ErrorLine { get; set; }

        /// <summary>
        /// 错误列号
        /// </summary>
        public int ErrorColumn { get; set; }

        /// <summary>
        /// 错误对象
        /// </summary>
        public Exception? Error { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SyntaxAnalysisResult()
        {
            Success = false;
            Tokens = null;
            Ast = null;
            ErrorMessage = null;
            ErrorLine = 0;
            ErrorColumn = 0;
            Error = null;
        }
    }
}