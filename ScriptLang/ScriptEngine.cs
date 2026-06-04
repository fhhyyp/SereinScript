using ScriptLang.Parser;
using ScriptLang.Prototype;
using ScriptLang.Runtime;

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

        public Scope GlobalScope { get; } = new Scope();

        /// <summary> 原型拓展管理器 </summary>
        public PrototypeManager PrototypeManager { get; }

        /// <summary>执行模式</summary>
        public ExecutionMode Mode { get; set; } = ExecutionMode.Compiled;

        /// <summary>编译缓存：AST -> ByteCodeChunk</summary>
        private readonly Dictionary<Expr, Runtime.ByteCode.ByteCodeChunk> _compilationCache = new();

        public ScriptEngine()
        {
            ImportResolver = new ImportResolver(this);
            PrototypeManager = new PrototypeManager(this);

            PrototypeManager.Register<ArrayPrototype>();
            PrototypeManager.Register<ObjectPrototype>();
            PrototypeManager.Register<StringPrototype>();

        }

        /// <summary>
        /// 执行脚本代码并返回结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="scope">提供注册方法</param>
        /// <returns></returns>
        public Task<Value> CreateTask(string filePath, Scope? scope = null)
        {
            BuiltinFunctions.RegisterAll(GlobalScope);
            if (!SourceManager.TryGetSource(filePath, out var script))
            {
                script = File.ReadAllText(filePath);
                SourceManager.AddSource(filePath, script);
            }
            if (Path.GetDirectoryName(filePath) is string rootPath)
            {
                ImportResolver.RootPath = rootPath;
            }
           
            // 词法分析获取 Token 列表
            var lexer = new Lexer.Lexer(script, filePath);
            var tokens = lexer.ScanTokens();
            // 语法分析获取 AST 
            var parser = new Parser.Parser(tokens, filePath);
            var expr = parser.Parse();
            // 判断是否有解析异常
            if (parser.Diagnostics.Count > 0)
            {
                for (int index = 0; index < parser.Diagnostics.Count; index++)
                {
                    ParseException? diagnostic = parser.Diagnostics[index];
                    Console.WriteLine($"第 {index + 1} 个异常 ：" + diagnostic.ToString());
                }
                throw new Exception($"Parser 阶段产生 {parser.Diagnostics.Count} 个异常");
            }
            if (scope is null)
            {
                scope = new Scope(GlobalScope);
            }
            if (Mode == ExecutionMode.Compiled)
            {
                return ExecuteCompiledAsync(expr, scope);
            }
            else
            {
                return ExecuteInterpretedAsync(expr, scope);
            }

           /* var chunk = _compiler.Compile(expr);
            var result = await _vm.ExecuteAsync(chunk);


            var interpreter = new Interpreter(this);
            var cts = interpreter.CancellationTokenSource;
            if(scope is null)
            {
                scope = new Scope(GlobalScope);
            }
        
            var evalTask = interpreter.MainEvaluateAsync(expr, scope);
            var scirptTask = new ScriptTask(evalTask, cts);

            

            return scirptTask;*/

        }

        /// <summary>
        /// 编译执行模式
        /// </summary>
        private async Task<Value> ExecuteCompiledAsync(Expr expr, Scope scope)
        {
            // 1. 获取或创建字节码缓存
            if (!_compilationCache.TryGetValue(expr, out var chunk))
            {
#if DEBUG
                var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
                // 编译 AST 到字节码
                var compiler = new Runtime.ByteCode.Compiler();
                
                chunk = compiler.Compile(expr);
                _compilationCache[expr] = chunk;

#if DEBUG
                sw.Stop();
                Console.WriteLine($"[Compile] 编译耗时: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"[Compile] 字节码指令数: {chunk.Code.Count}");
                Console.WriteLine($"[Compile] 常量数: {chunk.ConstantCount}");
#endif
            }

            // 2. 将外部作用域的变量注入到 VM 全局作用域
            //SyncScope(scope, _vm.GlobalScope);

            // 3. VM 执行字节码
#if true
            var vmSw = System.Diagnostics.Stopwatch.StartNew();
#endif
            var vm = new Runtime.ByteCode.VM(this);
            var result = await vm.ExecuteAsync(chunk);

#if true
            vmSw.Stop();
            Console.WriteLine($"[VM] 执行耗时: {vmSw.ElapsedMilliseconds}ms");
#endif

            return result;
        }


        /// <summary>
        /// 解释执行模式（保留原有的 Interpreter）
        /// </summary>
        private async Task<Value> ExecuteInterpretedAsync(Expr expr, Scope scope)
        {
            var interpreter = new Interpreter(this);
            var vmSw = System.Diagnostics.Stopwatch.StartNew();
            var result = await interpreter.MainEvaluateAsync(expr, scope);
            vmSw.Stop();
            Console.WriteLine($"[Interpreter] 执行耗时: {vmSw.ElapsedMilliseconds}ms");
            return result;
        }


        /// <summary>
        /// 加载模块并返回结果
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>

        internal async Task<Value> RunModuleAsync(string filePath, Scope scope)
        {
            var value = await CreateTask(filePath, scope); // .RunAsync();
            return value;
        }

        /*public async Task<Value> InvokeCallableAsync(ICallable callable, params List<Value> args)
        {
            if(callable is FunctionValue function)
            {
               var result = await function.CallAsync(this, args);
                return result;
            }
            else if(callable is CompiledFunctionValue compiledFunction)
            {
                var result = await compiledFunction.CallAsync(this, args);
                return result;
            }
            throw new RuntimeException("对象不可调用");
        }*/

        /*/// <summary>
        /// 执行脚本代码并返回结果
        /// </summary>
        /// <param name="script">代码</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="action">提供注册方法</param>
        /// <returns></returns>
        public async Task<Value> CreateTask2(string filePath,  Scope? scope = null)
        {
            if (string.IsNullOrWhiteSpace(MainDirectory) && Path.GetDirectoryName(filePath) is string mainDir)
            {
                MainDirectory = mainDir;
            }
            else
            {
                throw new Exception("当前正在运行");
            }
            GlobalScope.Clear();
            BuiltinFunctions.RegisterAll(GlobalScope);

            var script = await File.ReadAllTextAsync(filePath);

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
            MainDirectory = string.Empty;
            return evalResult.Value;
        }
*/



    }


}
