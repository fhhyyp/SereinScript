using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ScriptLang.Parser;
using System.Text.RegularExpressions;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SereinScript.LSP.Handlers
{
    /// <summary>
    /// 跳转定义处理器
    /// </summary>
    public class DefinitionHandler : IDefinitionHandler
    {
        private readonly DocumentManager _documentManager;
        private readonly SyntaxAnalyzer _syntaxAnalyzer;

        public DefinitionHandler(DocumentManager documentManager, SyntaxAnalyzer syntaxAnalyzer)
        {
            _documentManager = documentManager;
            _syntaxAnalyzer = syntaxAnalyzer;
        }



        public DefinitionRegistrationOptions GetRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DefinitionRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter
                {
                    Language = "sereinscript",
                    Scheme = "file"
                }
        )
            };
        }

        /// <summary>
        /// 处理跳转定义请求
        /// </summary>
        public async Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken) {
            Logger.Info($"Handling definition request for: {request.TextDocument.Uri} at position {request.Position.Line}:{request.Position.Character}", "Definition");
            var uri = request.TextDocument.Uri.ToString();
            var locations = await FindDefinitionsAsync(uri, request.Position);
            return locations.Count > 0 ? new LocationOrLocationLinks() : null;
        }

        /// <summary>
        /// 查找定义
        /// </summary>
        public async Task<List<Location>> FindDefinitionsAsync(string uri, Position position)
        {
            Logger.Info($"Finding definitions for: {uri} at position {position.Line}:{position.Character}", "Definition");
            var locations = new List<Location>();
            var content = _documentManager.GetDocumentContent(uri);

            if (string.IsNullOrEmpty(content))
            {
                Logger.Debug("Empty document content, returning empty locations", "Definition");
                return locations;
            }

            // 获取光标所在的标识符
            var identifier = GetIdentifierAtPosition(content, position);
            if (string.IsNullOrEmpty(identifier))
            {
                Logger.Debug("No identifier at position, returning empty locations", "Definition");
                return locations;
            }

            Logger.Info($"Searching for definition of: {identifier}", "Definition");
            
            // 使用 SyntaxAnalyzer 进行语法分析，获取 AST
            var analysisResult = _syntaxAnalyzer.PerformSyntaxAnalysis(content, uri);
            if (analysisResult.Success && analysisResult.Ast != null)
            {
                // 从 AST 中查找定义
                Logger.Debug("Searching definitions from AST", "Definition");
                FindDefinitionsFromAst(analysisResult.Ast, identifier, locations, uri);
                Logger.Info($"Found {locations.Count} definitions from AST", "Definition");
            }
            else
            {
                // 如果语法分析失败，使用基于正则表达式的方法查找定义
                Logger.Warning("Syntax analysis failed, using regex-based definition search", "Definition");
                FindVariableDefinition(content, identifier, locations, uri);
                FindFunctionDefinition(content, identifier, locations, uri);
                Logger.Info($"Found {locations.Count} definitions using regex", "Definition");
            }

            Logger.Info($"Returning {locations.Count} definitions", "Definition");
            return locations;
        }

        /// <summary>
        /// 从 AST 中查找定义
        /// </summary>
        private void FindDefinitionsFromAst(object ast, string identifier, List<Location> locations, string uri)
        {
            if (ast is ScriptLang.Parser.Program program)
            {
                foreach (var statement in program.Statements)
                {
                    FindDefinitionFromExpr(statement, identifier, locations, uri);
                }
            }
            else if (ast is Expr expr)
            {
                FindDefinitionFromExpr(expr, identifier, locations, uri);
            }
        }

        /// <summary>
        /// 从表达式中查找定义
        /// </summary>
        private void FindDefinitionFromExpr(Expr expr, string identifier, List<Location> locations, string uri)
        {
            switch (expr)
            {
                case LetExpr letExpr:
                    // 检查变量声明
                    if (letExpr.Name == identifier)
                    {
                        locations.Add(new Location
                        {
                            Uri = uri,
                            Range = CreateRangeFromSourceSpan(letExpr.SourceSpan)
                        });
                    }
                    // 递归查找值表达式中的定义
                    FindDefinitionFromExpr(letExpr.Value, identifier, locations, uri);
                    break;

                case VarExpr varExpr:
                    // 检查变量声明
                    if (varExpr.Name == identifier)
                    {
                        locations.Add(new Location
                        {
                            Uri = uri,
                            Range = CreateRangeFromSourceSpan(varExpr.SourceSpan)
                        });
                    }
                    // 递归查找值表达式中的定义
                    FindDefinitionFromExpr(varExpr.Value, identifier, locations, uri);
                    break;

                case ForExpr forExpr:
                    // 检查循环变量
                    if (forExpr.VarName == identifier)
                    {
                        locations.Add(new Location
                        {
                            Uri = uri,
                            Range = CreateRangeFromSourceSpan(forExpr.SourceSpan)
                        });
                    }
                    // 递归查找迭代器和循环体中的定义
                    FindDefinitionFromExpr(forExpr.Iterable, identifier, locations, uri);
                    FindDefinitionFromExpr(forExpr.Body, identifier, locations, uri);
                    break;

                case ObjectLiteralExpr objectExpr:
                    // 检查对象属性
                    foreach (var property in objectExpr.Properties)
                    {
                        if (property.Key == identifier)
                        {
                            locations.Add(new Location
                            {
                                Uri = uri,
                                Range = CreateRangeFromSourceSpan(property.SourceSpan)
                            });
                        }
                        // 递归查找属性值中的定义
                        FindDefinitionFromExpr(property.Value, identifier, locations, uri);
                    }
                    break;

                case BlockExpr blockExpr:
                    // 递归查找代码块中的所有语句
                    foreach (var statement in blockExpr.Statements)
                    {
                        FindDefinitionFromExpr(statement, identifier, locations, uri);
                    }
                    break;

                case IfExpr ifExpr:
                    // 递归查找条件表达式
                    FindDefinitionFromExpr(ifExpr.Cond, identifier, locations, uri);
                    FindDefinitionFromExpr(ifExpr.Then, identifier, locations, uri);
                    FindDefinitionFromExpr(ifExpr.Else, identifier, locations, uri);
                    break;

                case BinaryExpr binaryExpr:
                    // 递归查找二元表达式的左右操作数
                    FindDefinitionFromExpr(binaryExpr.Left, identifier, locations, uri);
                    FindDefinitionFromExpr(binaryExpr.Right, identifier, locations, uri);
                    break;

                case CallExpr callExpr:
                    // 递归查找函数调用的目标和参数
                    FindDefinitionFromExpr(callExpr.Target, identifier, locations, uri);
                    foreach (var arg in callExpr.Args)
                    {
                        FindDefinitionFromExpr(arg, identifier, locations, uri);
                    }
                    break;

                case MemberAccessExpr memberExpr:
                    // 检查成员访问的属性名
                    if (memberExpr.Property == identifier)
                    {
                        // 这里可以添加对成员属性定义的查找
                    }
                    // 递归查找成员访问的目标
                    FindDefinitionFromExpr(memberExpr.Target, identifier, locations, uri);
                    break;

                case IndexAccessExpr indexExpr:
                    // 递归查找索引访问的目标和索引
                    FindDefinitionFromExpr(indexExpr.Target, identifier, locations, uri);
                    FindDefinitionFromExpr(indexExpr.Index, identifier, locations, uri);
                    break;

                case ArrayLiteralExpr arrayExpr:
                    // 递归查找数组元素
                    foreach (var element in arrayExpr.Elements)
                    {
                        FindDefinitionFromExpr(element, identifier, locations, uri);
                    }
                    break;

                case ConditionalExpr conditionalExpr:
                    // 递归查找条件表达式的各个部分
                    FindDefinitionFromExpr(conditionalExpr.Cond, identifier, locations, uri);
                    FindDefinitionFromExpr(conditionalExpr.Then, identifier, locations, uri);
                    FindDefinitionFromExpr(conditionalExpr.Else, identifier, locations, uri);
                    break;

                case UnaryExpr unaryExpr:
                    // 递归查找一元表达式的操作数
                    FindDefinitionFromExpr(unaryExpr.Expr, identifier, locations, uri);
                    break;

                case ReturnExpr returnExpr:
                    // 递归查找返回值
                    if (returnExpr.Value != null)
                    {
                        FindDefinitionFromExpr(returnExpr.Value, identifier, locations, uri);
                    }
                    break;

                case WhenExpr whenExpr:
                    // 递归查找 when 表达式的各个部分
                    FindDefinitionFromExpr(whenExpr.Value, identifier, locations, uri);
                    foreach (var clause in whenExpr.Clauses)
                    {
                        FindDefinitionFromExpr(clause.Pattern, identifier, locations, uri);
                        FindDefinitionFromExpr(clause.Body, identifier, locations, uri);
                    }
                    break;

                case LambdaExpr lambdaExpr:
                    // 检查 lambda 表达式的参数
                    for (int i = 0; i < lambdaExpr.Params.Count; i++)
                    {
                        if (lambdaExpr.Params[i] == identifier)
                        {
                            locations.Add(new Location
                            {
                                Uri = uri,
                                Range = CreateRangeFromSourceSpan(lambdaExpr.SourceSpan)
                            });
                        }
                    }
                    // 递归查找 lambda 表达式的 body
                    FindDefinitionFromExpr(lambdaExpr.Body, identifier, locations, uri);
                    break;

                case ImportStmt importStmt:
                    // 检查导入的成员
                    foreach (var member in importStmt.Members)
                    {
                        if (member == identifier)
                        {
                            locations.Add(new Location
                            {
                                Uri = uri,
                                Range = CreateRangeFromSourceSpan(importStmt.SourceSpan)
                            });
                        }
                    }
                    break;

                case IdentifierExpr identifierExpr:
                    // 检查标识符引用
                    if (identifierExpr.Name == identifier)
                    {
                        // 这里可以添加对标识符引用的处理
                    }
                    break;

                case AssignExpr assignExpr:
                    // 检查赋值目标
                    if (assignExpr.Name == identifier)
                    {
                        // 这里可以添加对赋值目标的处理
                    }
                    // 递归查找赋值表达式的值
                    FindDefinitionFromExpr(assignExpr.Value, identifier, locations, uri);
                    break;

                case MemberAssignExpr memberAssignExpr:
                    // 检查成员赋值的属性名
                    if (memberAssignExpr.Property == identifier)
                    {
                        // 添加对成员属性赋值的处理
                        locations.Add(new Location
                        {
                            Uri = uri,
                            Range = CreateRangeFromSourceSpan(memberAssignExpr.SourceSpan)
                        });
                    }
                    // 递归查找成员赋值的目标和值
                    FindDefinitionFromExpr(memberAssignExpr.Target, identifier, locations, uri);
                    FindDefinitionFromExpr(memberAssignExpr.Value, identifier, locations, uri);
                    break;

                case IndexAssignExpr indexAssignExpr:
                    // 递归查找索引赋值的目标、索引和值
                    FindDefinitionFromExpr(indexAssignExpr.Target, identifier, locations, uri);
                    FindDefinitionFromExpr(indexAssignExpr.Index, identifier, locations, uri);
                    FindDefinitionFromExpr(indexAssignExpr.Value, identifier, locations, uri);
                    break;

                case LiteralExpr:
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// 获取光标位置的标识符
        /// </summary>
        private string GetIdentifierAtPosition(string content, Position position)
        {
            var lines = content.Split('\n');
            if (position.Line >= lines.Length)
            {
                return string.Empty;
            }

            var currentLine = lines[position.Line];
            if (position.Character >= currentLine.Length)
            {
                return string.Empty;
            }

            // 查找光标位置的标识符
            var start = position.Character;
            var end = position.Character;

            // 向左查找标识符开始
            while (start > 0 && IsIdentifierChar(currentLine[start - 1]))
            {
                start--;
            }

            // 向右查找标识符结束
            while (end < currentLine.Length && IsIdentifierChar(currentLine[end]))
            {
                end++;
            }

            return currentLine.Substring(start, end - start);
        }

        /// <summary>
        /// 检查字符是否为标识符字符
        /// </summary>
        private bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '@';
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
        /// 查找变量定义
        /// </summary>
        private void FindVariableDefinition(string content, string identifier, List<Location> locations, string uri)
        {
            // 匹配变量声明，如: let a = 10 或 var a = 10
            var variablePattern = new Regex($@"^\s*(let|var)\s+({identifier})\s*=", RegexOptions.Multiline);
            var match = variablePattern.Match(content);

            if (match.Success)
            {
                var line = GetLineNumber(content, match.Index);
                var column = GetColumnNumber(content, match.Index, line);
                var keyword = match.Groups[1].Value;

                locations.Add(new Location
                {
                    Uri = uri,
                    Range = new LspRange
                    {
                        Start = new LspPosition { Line = line - 1, Character = column - 1 + keyword.Length + 1 },
                        End = new LspPosition { Line = line - 1, Character = column - 1 + keyword.Length + 1 + identifier.Length }
                    }
                });
            }
        }

        /// <summary>
        /// 查找函数定义
        /// </summary>
        private void FindFunctionDefinition(string content, string identifier, List<Location> locations, string uri)
        {
            // 匹配函数定义，如: let add = (a, b) => a + b
            var functionPattern = new Regex($@"^\s*(let|var)\s+({identifier})\s*=\s*\(([^)]*)\)\s*=>", RegexOptions.Multiline);
            var match = functionPattern.Match(content);

            if (match.Success)
            {
                var line = GetLineNumber(content, match.Index);
                var column = GetColumnNumber(content, match.Index, line);
                var keyword = match.Groups[1].Value;

                locations.Add(new Location
                {
                    Uri = uri,
                    Range = new LspRange
                    {
                        Start = new LspPosition { Line = line - 1, Character = column - 1 + keyword.Length + 1 },
                        End = new LspPosition { Line = line - 1, Character = column - 1 + keyword.Length + 1 + identifier.Length }
                    }
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

