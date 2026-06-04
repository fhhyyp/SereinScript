using ScriptLang.Parser;
using ScriptLang.Prototype;
using ScriptLang.Runtime;
using ScriptLang.Runtime.ByteCode;

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
        /// 全局作用域，此作用域应只注册
        /// </summary>
        public Scope GlobalScope { get; } = new Scope();

        /// <summary>
        /// 原型拓展管理器
        /// </summary>
        public PrototypeManager PrototypeManager { get; }

        /// <summary>
        /// 编译缓存：AST → ByteCodeChunk
        /// </summary>
        private readonly Dictionary<Expr, ByteCodeChunk> _compilationCache = new();

        /// <summary>
        /// VM 实例（可重用，全局变量值通过 GlobalSlotRegistry 共享）
        /// </summary>
        private VM? _vm;

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
        /// <param name="scope">提供注册方法（可选，用于注入外部变量）</param>
        /// <returns></returns>
        public ScriptTask CreateTask(string filePath, Scope? scope = null)
        {
            GlobalScope.Clear();
            BuiltinFunctions.RegisterAll(GlobalScope);

            if (!SourceManager.TryGetSource(filePath, out var script))
            {
                script = File.ReadAllText(filePath);
#if DEBUG
                Console.WriteLine("----------------------------------");
                Console.WriteLine($"[Lexer]准备解析脚本：{filePath}");
                Console.WriteLine(script);
                Console.WriteLine();
                Console.WriteLine("----------------------------------"); 
#endif
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

            // 检查解析异常
            if (parser.Diagnostics.Count > 0)
            {
                for (int index = 0; index < parser.Diagnostics.Count; index++)
                {
                    ParseException? diagnostic = parser.Diagnostics[index];
                    Console.WriteLine($"第 {index + 1} 个异常 ：" + diagnostic.ToString());
                }
                throw new Exception($"Parser 阶段产生 {parser.Diagnostics.Count} 个异常");
            }

            if (scope == null)
            {
                scope = new Scope(GlobalScope);
            }

            // 将外部作用域变量注册到 GlobalSlotRegistry
            RegisterExternalScopeToGlobalSlots(scope);

            return CreateCompiledTask(expr);
        }

        /// <summary>
        /// 将外部作用域中的变量注册到 GlobalSlotRegistry
        /// </summary>
        private static void RegisterExternalScopeToGlobalSlots(Scope scope)
        {
            // 遍历作用域链中的所有变量，注册到全局槽位表
            Scope? current = scope;
            while (current != null)
            {
                // Scope 没有直接暴露变量列表，通过反射或添加 API
                // 此处假设外部注入的变量已经在 GlobalScope 中
                current = current.Parent as Scope;
            }
        }

        /// <summary>
        /// 编译执行模式（唯一执行路径）
        /// </summary>
        private ScriptTask CreateCompiledTask(Expr expr)
        {
            // 获取或创建字节码缓存
            if (!_compilationCache.TryGetValue(expr, out var chunk))
            {
#if DEBUG
                var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
                var compiler = new Compiler();
                chunk = compiler.Compile(expr);
                _compilationCache[expr] = chunk;

#if DEBUG
                sw.Stop();
                Console.WriteLine($"[Compile] 编译耗时: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"[Compile] 字节码指令数: {chunk.Code.Count}");
                Console.WriteLine($"[Compile] 常量数: {chunk.ConstantCount}");
                if (chunk.VariableTable != null)
                {
                    var vt = chunk.VariableTable;
                    Console.WriteLine($"[Compile] 变量表: L={vt.LocalCount} C={vt.CaptureCount} G={vt.GlobalCount} B={vt.BuiltinCount}");
                }
#endif
            }

            // 创建执行工厂
            Func<Task<Value>> factory = new Func<Task<Value>>(async () =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
#if DEBUG
#endif
                // 每次执行创建新的 VM 实例（保证栈/帧隔离）
                var vm = new VM(this);
                var result = await vm.ExecuteAsync(chunk);
                sw.Stop();
                Console.WriteLine($"[VM] 执行耗时: {sw.ElapsedMilliseconds}ms");
#if DEBUG
#endif
                return result;
            });

            ScriptTask scriptTask = new ScriptTask(factory, new CancellationTokenSource());
            return scriptTask;
        }

        /// <summary>
        /// 加载模块并返回结果
        /// </summary>
        internal async Task<Value> RunModuleAsync(string filePath, Scope scope)
        {
            var value = await CreateTask(filePath, scope).RunAsync();
            return value;
        }

        /// <summary>
        /// 清除编译缓存
        /// </summary>
        public void ClearCache()
        {
            _compilationCache.Clear();
            GlobalSlotRegistry.Reset();
        }

        /// <summary>
        /// 预注册外部全局变量（编译前调用）
        /// </summary>
        public void RegisterGlobal(string name)
        {
            GlobalSlotRegistry.Register(name);
        }

        /// <summary>
        /// 设置全局变量值
        /// </summary>
        public void SetGlobal(string name, Value value)
        {
            int slot = GlobalSlotRegistry.GetSlot(name);
            GlobalSlotRegistry.SetValue(slot, value);
        }
    }

    /// <summary>
    /// 创建一个执行任务，该任务可重复执行
    /// </summary>
    public sealed class ScriptTask(Func<Task<Value>> task, CancellationTokenSource cts)
    {
        private readonly Func<Task<Value>> task = task;

        public CancellationToken Token => cts.Token;

        public bool IsCanceled => Token.IsCancellationRequested;

        public async Task<Value> RunAsync() => await task.Invoke();

        public void Cancel() => cts.Cancel();
    }
}