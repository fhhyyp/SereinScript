using ScriptLang.Lexer;
using ScriptLang.Parser;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ScriptLang
{
    public sealed class ScriptEngine
    {
        /// <summary>
        /// 脚本依赖导入工具
        /// </summary>
        public ImportResolver ImportResolver { get; private set; } 

        /// <summary>
        /// 脚本源文件管理
        /// </summary>
        public SourceManager SourceManager { get; } = new SourceManager();

        /// <summary>
        /// 默认的全局作用域
        /// </summary>
        public Scope GlobalScope { get; } = new Scope();

        /// <summary>
        /// 脚本主路径
        /// </summary>
        public string MainDirectory { get; private set; } = string.Empty;

        public ScriptEngine()
        {
            ImportResolver = new ImportResolver(this);
            BuiltinFunctions.RegisterAll(GlobalScope);
        }

        /// <summary>
        /// 执行脚本代码并返回结果
        /// </summary>
        /// <param name="script">代码</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="action">提供注册方法</param>
        /// <returns></returns>
        public async Task<Value> RunAsync(string filePath,  Scope? scope = null)
        {
            var script = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(MainDirectory) && Path.GetDirectoryName(filePath) is string mainDir)
            {
                MainDirectory = mainDir;
            }

            SourceManager.AddSource(filePath, script);

            var lexer = new Lexer.Lexer(script, filePath);
            var tokens = lexer.ScanTokens();

            var parser = new Parser.Parser(tokens, filePath);
            var ast = parser.Parse();

            if (parser.Diagnostics.Count > 0) 
            {
                for (int index = 0; index < parser.Diagnostics.Count; index++)
                {
                    ParseException? diagnostic = parser.Diagnostics[index];
                    Console.WriteLine($"第 {index + 1} 个异常 ：" + diagnostic.ToString());
                }
                throw new Exception($"Parser 阶段产生 {parser.Diagnostics.Count} 个异常");
            }

            var interpreter = new Interpreter(this);
            var evalResult = await interpreter.EvaluateAsync(ast, scope ?? GlobalScope);
            return evalResult.Value;
        }


        /// <summary>
        /// 由 <see cref="FunctionValue.CallAsync(ScriptEngine, List{Value})"/>
        /// 执行 AST 并返回 Value
        /// </summary>
        public async Task<Value> EvaluateAsync(Expr expr, Scope? scope = null)
        {
            var interpreter = new Interpreter(this);
            var evalResult = await interpreter.EvaluateAsync(expr, scope ?? GlobalScope);
            return evalResult.Value;
        }


    }


}
