using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace SereinScript.LSP
{
    /// <summary>
    /// Samples 目录解析器，用于分析 DSL 语法和提取关键字
    /// </summary>
    public class SamplesParser
    {
        private readonly string _samplesDirectory;

        public SamplesParser(string samplesDirectory)
        {
            _samplesDirectory = samplesDirectory;
        }

        /// <summary>
        /// 分析 Samples 目录中的所有脚本文件
        /// </summary>
        public async Task<DSLSyntaxInfo> ParseSamplesAsync()
        {
            var syntaxInfo = new DSLSyntaxInfo();

            // 读取所有 .script 文件
            var scriptFiles = Directory.GetFiles(_samplesDirectory, "*.script", SearchOption.AllDirectories);

            foreach (var file in scriptFiles)
            {
                await ParseScriptFileAsync(file, syntaxInfo);
            }

            return syntaxInfo;
        }

        /// <summary>
        /// 解析单个脚本文件
        /// </summary>
        private async Task ParseScriptFileAsync(string filePath, DSLSyntaxInfo syntaxInfo)
        {
            var content = await File.ReadAllTextAsync(filePath);

            // 提取关键字
            ExtractKeywords(content, syntaxInfo);

            // 提取函数定义
            ExtractFunctions(content, syntaxInfo);

            // 提取变量声明
            ExtractVariables(content, syntaxInfo);

            // 提取语法模式
            ExtractSyntaxPatterns(content, syntaxInfo);
        }

        /// <summary>
        /// 提取关键字
        /// </summary>
        private void ExtractKeywords(string content, DSLSyntaxInfo syntaxInfo)
        {
            // 基础关键字
            var keywords = new[] { "let", "var", "if", "then" , "when", "else", "for", /*"while", "function",*/ "return", "true", "false", "null" };

            foreach (var keyword in keywords)
            {
                if (Regex.IsMatch(content, $@"\b{keyword}\b"))
                {
                    syntaxInfo.Keywords.Add(keyword);
                }
            }
        }

        /// <summary>
        /// 提取函数定义
        /// </summary>
        private void ExtractFunctions(string content, DSLSyntaxInfo syntaxInfo)
        {
            // 匹配函数定义，如: let add = (a, b) => a + b
            var functionPattern = new Regex(@"let\s+(\w+)\s*=\s*\(([^)]*)\)\s*=>", RegexOptions.Multiline);
            var matches = functionPattern.Matches(content);

            foreach (Match match in matches)
            {
                var functionName = match.Groups[1].Value;
                var parameters = match.Groups[2].Value.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

                syntaxInfo.Functions.Add(new DSLFunction
                {
                    Name = functionName,
                    Parameters = parameters
                });
            }
        }

        /// <summary>
        /// 提取变量声明
        /// </summary>
        private void ExtractVariables(string content, DSLSyntaxInfo syntaxInfo)
        {
            // 匹配变量声明，如: let a = 10
            var variablePattern = new Regex(@"let\s+(\w+)\s*=", RegexOptions.Multiline);
            var matches = variablePattern.Matches(content);

            foreach (Match match in matches)
            {
                var variableName = match.Groups[1].Value;
                syntaxInfo.Variables.Add(variableName);
            }
        }

        /// <summary>
        /// 提取语法模式
        /// </summary>
        private void ExtractSyntaxPatterns(string content, DSLSyntaxInfo syntaxInfo)
        {
            // 提取各种语法模式，用于补全和文档
            // TODO: 实现更复杂的语法模式提取
        }
    }

    /// <summary>
    /// DSL 语法信息
    /// </summary>
    public class DSLSyntaxInfo
    {
        public HashSet<string> Keywords { get; } = new();
        public List<DSLFunction> Functions { get; } = new();
        public HashSet<string> Variables { get; } = new();
        public List<string> SyntaxPatterns { get; } = new();
    }

    /// <summary>
    /// DSL 函数信息
    /// </summary>
    public class DSLFunction
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Parameters { get; set; } = new();
    }
}