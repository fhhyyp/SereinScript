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
        public ImportResolver ImportResolver => _importResolver;
        public SourceManager SourceManager => _sourceManager;
        public Scope GlobalScope => _globalScope;

        private readonly SourceManager _sourceManager = new SourceManager();
        private readonly Scope _globalScope = new Scope();
        private readonly ImportResolver _importResolver;
        private readonly string _baseDirectory;

        public ScriptEngine(string? baseDirectory =  null)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory =  Directory.GetCurrentDirectory();
            }
            BuiltinFunctions.RegisterAll(_globalScope);
            _baseDirectory = baseDirectory;
            _importResolver = new ImportResolver(this, baseDirectory);
        }


        public async Task<Value> LoadAndRunAsync(string scriptFile, Action<Scope>? action = null)
        {
            if (!File.Exists(scriptFile))
            {
                var f = Path.Combine(_baseDirectory, scriptFile);
                if (!File.Exists(f))
                {
                    throw new Exception($"Not found script file : {scriptFile}");
                }
                scriptFile = f;
            }
            var script = await File.ReadAllTextAsync(scriptFile);
            var value = await RunAsync(script, scriptFile, action);
            return value;
        }

        /// <summary>
        /// 由 <see cref="FunctionValue.CallAsync(ScriptEngine, List{Value})"/>
        /// 执行 AST 并返回 Value
        /// </summary>
        public async Task<Value> EvaluateAsync(Expr expr, Scope? scope = null)
        {
            var interpreter = new Interpreter(this);
            var evalResult = await  interpreter.EvaluateAsync(expr, scope ?? _globalScope);
            return evalResult.Value;
        }

        /// <summary>
        /// 执行脚本代码并返回结果
        /// </summary>
        /// <param name="script">代码</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="action">提供注册方法</param>
        /// <returns></returns>
        public async Task<Value> RunAsync(string script, string filePath = "<memory>", Action<Scope>? action = null)
        {
            _sourceManager.AddSource(filePath, script);

            var lexer = new Lexer.Lexer(script, filePath);
            var tokens = lexer.ScanTokens();

            var parser = new Parser.Parser(tokens, filePath);
            var ast = parser.Parse();


            var interpreter = new Interpreter(this);
            var evalResult = await interpreter.EvaluateAsync(ast, _globalScope);
            return evalResult.Value;
        }

    }


}
