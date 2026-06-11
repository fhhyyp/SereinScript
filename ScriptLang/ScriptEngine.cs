using ScriptLang.Parser;
using ScriptLang.Prototype;
using ScriptLang.Runtime;
using ScriptLang.Runtime.ByteCode;
using System.Diagnostics;
using System.Reflection.Metadata;

namespace ScriptLang
{
    public sealed class ScriptEngine
    {
        public bool IsPrintVMInfo { get;  set; } =
#if debug
            true;
#else
            false;
#endif
        public bool IsPrintInputSciptContent { get;  set; } = false;

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
        private readonly Dictionary<Expr, ByteCodeChunk> _compilationCache = [];

        public ScriptEngine()
        {
            PrototypeManager = new PrototypeManager(this);
            ImportResolver = new ImportResolver(this);
            PrototypeManager.Register<ArrayPrototype>();
            PrototypeManager.Register<ObjectPrototype>();
            PrototypeManager.Register<StringPrototype>();
            PrototypeManager.Register<DateTimePrototype>();
            PrototypeManager.Register<TimeSpanPrototype>();

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
            BuiltinCache.RegisterAll(GlobalScope);
            if (!SourceManager.TryGetSource(filePath, out var script))
            {
                script = File.ReadAllText(filePath);
                if (IsPrintInputSciptContent)
                {
                    Console.WriteLine("================加载脚本==============");
                    Console.WriteLine($"# 脚本路径：{filePath}");
                    Console.WriteLine(script);
                    Console.WriteLine("================解析完毕==============");
                    Console.WriteLine("");
#if DEBUG
                    Debug.WriteLine("================加载脚本==============");
                    Debug.WriteLine($"# 脚本路径：{filePath}");
                    Debug.WriteLine(script);
                    Debug.WriteLine("================解析完毕==============");
                    Debug.WriteLine("");
#endif
                }

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
#if DEBUG
                    Debug.WriteLine($"第 {index + 1} 个异常 ：" + diagnostic.ToString());
#endif
                }
                throw new Exception($"Parser 阶段产生 {parser.Diagnostics.Count} 个异常");
            }

            scope ??= new Scope(GlobalScope);

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
        /// 从源代码字符串创建执行任务（用于内存中的脚本代码，如 Excel 宏）
        /// </summary>
        /// <param name="source">脚本源代码字符串</param>
        /// <param name="sourceName">源名称（用于错误报告，如宏名称）</param>
        /// <param name="scope">外部作用域（可选，用于注入 Excel 对象等）</param>
        /// <returns>可执行的 ScriptTask</returns>
        public ScriptTask CreateTaskFromSource(string source, string sourceName, Scope? scope = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(sourceName);

            GlobalScope.Clear();
            BuiltinCache.RegisterAll(GlobalScope);

            // 注册源代码（复用 SourceManager，但来源是内存而非文件）
            SourceManager.AddSource(sourceName, source);

            // 词法分析
            var lexer = new Lexer.Lexer(source, sourceName);
            var tokens = lexer.ScanTokens();

            // 语法分析
            var parser = new Parser.Parser(tokens, sourceName);
            var expr = parser.Parse();

            // 检查解析异常
            if (parser.Diagnostics.Count > 0)
            {
                var messages = parser.Diagnostics.Select(d => d.ToString());
                throw new Exception($"脚本解析错误 ({sourceName}):\n{string.Join("\n", messages)}");
            }

            // 构建执行作用域
            scope ??= new Scope(GlobalScope);
            RegisterExternalScopeToGlobalSlots(scope);

            // 编译并创建任务
            return CreateCompiledTask(expr);
        }

        /// <summary>
        /// 从已编译的 ByteCodeChunk 创建执行任务（跳过编译阶段，直接从 .ssc 文件加载后使用）
        /// </summary>
        /// <param name="chunk">已反序列化的字节码块</param>
        /// <param name="filePath">.ssc 文件路径（可选，用于 import 模块路径解析）。传入后自动设置 ImportResolver.RootPath</param>
        /// <returns>可重复执行的 ScriptTask</returns>
        public ScriptTask CreateTask(ByteCodeChunk chunk, string? filePath = null)
        {
            ArgumentNullException.ThrowIfNull(chunk);

            // 从 .ssc 文件路径推导 import 模块根目录（编译产物本身不含路径信息，保持可移植性）
            if (filePath != null && Path.GetDirectoryName(Path.GetFullPath(filePath)) is string rootPath)
            {
                ImportResolver.RootPath = rootPath;
            }

            // 从 VariableTable 恢复全局变量注册
            var vt = chunk.VariableTable;
            if (vt != null && vt.GlobalCount > 0)
            {
                foreach (var name in vt.GlobalNames)
                {
                    GlobalSlotRegistry.Register(name);
                }
                GlobalSlotRegistry.InitializeValues();
            }

            // 创建执行工厂
            Func<Task<Value>> factory = new Func<Task<Value>>(async () =>
            {
                var sw = Stopwatch.StartNew();
                var vm = new VM(this);
                var result = await vm.ExecuteAsync(chunk);
                sw.Stop();
                Console.WriteLine($"[VM] 执行耗时: {sw.ElapsedMilliseconds}ms");
                return result;
            });

            return new ScriptTask(factory, new CancellationTokenSource());
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
                var sw = Stopwatch.StartNew();
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
#if DEBUG
                var sw = Stopwatch.StartNew();
#endif
                // 每次执行创建新的 VM 实例（保证栈/帧隔离）
                var vm = new VM(this);
                var result = await vm.ExecuteAsync(chunk);
#if DEBUG
                sw.Stop();
                Console.WriteLine($"[VM] 执行耗时: {sw.ElapsedMilliseconds}ms");
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
        public static void RegisterGlobal(string name)
        {
            GlobalSlotRegistry.Register(name);
        }

        /// <summary>
        /// 设置全局变量值
        /// </summary>
        public static void SetGlobal(string name, Value value)
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