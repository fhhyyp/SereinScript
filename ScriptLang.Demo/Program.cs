using ScriptLang.Runtime;
using ScriptLang.Runtime.ByteCode;
using System.Diagnostics;
using System.Linq;

namespace ScriptLang.Demo;

class Program
{
    static async Task Main(string[] args)
    {
        string[][] scripts =
        [[
            @".\Samples\1\1.1-基础运算.script",
            @".\Samples\1\1.2-变量声明.script",
            @".\Samples\1\1.3-字符串操作.script",
            @".\Samples\1\1.4-函数.script",
            @".\Samples\1\1.5-对象.script",
            @".\Samples\1\1.6-复杂对象.script",
            @".\Samples\1\1.7-数组.script",
            @".\Samples\1\1.8-数值数据类型.script",
        ],[
            @".\Samples\2\2.1-逻辑运算.script",
            @".\Samples\2\2.2-条件表达式.script",
            @".\Samples\2\2.3-循环.script",
            @".\Samples\2\2.4-模式匹配.script",
        ],
        [
            @".\Samples\3\3.1-闭包.script",
            @".\Samples\3\3.2-高阶函数.script",
            @".\Samples\3\3.3-递归.script",
            @".\Samples\3\3.4-快速排序.script",
            @".\Samples\3\3.5-矩阵运算.script",
        ],
        [
            @".\Samples\4\4.1-CLR对象.script",
            @".\Samples\4\4.2-异步调用.script",
            @".\Samples\4\4.3-CLR回调.script",
        ],
        [  // 5
          @".\Samples\test\test.script",
          @".\Samples\test\test_closure_memory.script",
          @".\Samples\test\test-closure-recursion.script",
          @".\Samples\test\test-deep-recursion.script",
          @".\Samples\test\test-extreme-recursion.script",
          @".\Samples\test\test-mutual-recursion.script",
          @".\Samples\test\test-stack-overflow.script",
        ],
        [ // 6
            @".\Samples\高级\linq\run-linq.script",
            @".\Samples\高级\pinia\run-import.script",
        ],
        ];

        // 默认选择
        //scirpt(6, 1);

        if (args.Length == 0)
        {
            Console.WriteLine("用法:");
            Console.WriteLine("  ScriptLang.Demo <script-path>            直接执行脚本");
            Console.WriteLine("  ScriptLang.Demo --compare <script-path>  编译执行 vs 编译→保存→加载→执行 对比");
            Console.WriteLine("  ScriptLang.Demo --save <script-path>     编译并保存为 .ssc 文件");
            Console.WriteLine("  ScriptLang.Demo --load <ssc-path>        加载 .ssc 文件并执行");
            Console.WriteLine("  ScriptLang.Demo --bench <script-path>    批量对比所有预设脚本");
            return;
        }

        if (args[0] == "--compare" && args.Length >= 2)
        {
            await CompareMode(args[1]);
            return;
        }

        if (args[0] == "--save" && args.Length >= 2)
        {
            await SaveMode(args[1]);
            return;
        }

        if (args[0] == "--load" && args.Length >= 2)
        {
            await LoadMode(args[1]);
            return;
        }

        if (args[0] == "--bench")
        {
            await BenchMode(scripts);
            return;
        }

        // 默认模式：直接执行
        await RunScript(args[0]);

        void scirpt(int page, int index)
        {
            var script = scripts[page - 1][index - 1];
            args = [script];
        }
    }

    // ==================== 运行模式 ====================

    /// <summary>直接编译并执行脚本</summary>
    static async Task RunScript(string scriptPath)
    {
        var exePath = AppDomain.CurrentDomain.BaseDirectory;
        var fullPath = Path.GetFullPath(Path.Combine(exePath, scriptPath));
        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"文件不存在: {fullPath}");
            return;
        }

        var engine = CreateEngine();
        try
        {
            var sw = Stopwatch.StartNew();
            var task = engine.CreateTask(fullPath);
            var result = await task.RunAsync();
            sw.Stop();
            Console.WriteLine($"结果: {result}");
            Console.WriteLine($"耗时: {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"异常: {ex}");
        }
    }

    // ==================== 对比模式：编译执行 vs 编译→保存→加载→执行 ====================

    static async Task CompareMode(string scriptPath)
    {
        var exePath = AppDomain.CurrentDomain.BaseDirectory;
        var fullPath = Path.GetFullPath(Path.Combine(exePath, scriptPath));
        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"文件不存在: {fullPath}");
            return;
        }

        Console.WriteLine("========================================");
        Console.WriteLine($"对比测试: {Path.GetFileName(fullPath)}");
        Console.WriteLine("========================================");

        // 1. 直接编译执行（走 ScriptEngine.CreateTask(string) 完整流程）
        Console.WriteLine("\n--- [1] 直接编译执行 ---");
        GlobalSlotRegistry.Reset();
        var engine = CreateEngine();
        string? result1;
        try
        {
            var task1 = engine.CreateTask(fullPath);
            var value1 = await task1.RunAsync();
            result1 = value1?.ToString() ?? "null";
            Console.WriteLine($"结果: {result1}");
        }
        catch (Exception ex)
        {
            result1 = $"<异常: {ex.Message}>";
            Console.WriteLine($"异常: {ex}");
        }

        // 2. 编译 → 保存 → 加载 → 执行
        //    注意：复用同一个 engine 实例，避免 PrototypeManager 静态状态冲突
        Console.WriteLine("\n--- [2] 编译 → 保存 → 加载 → 执行 ---");
        GlobalSlotRegistry.Reset();
        engine.ClearCache();

        // 编译（独立编译器，不依赖 engine）
        var compiler2 = new Compiler();
        var ast2 = ParseScript(fullPath);
        var chunk2 = compiler2.Compile(ast2);

        // 保存
        string sscPath = Path.ChangeExtension(fullPath, ".ssc");
        ByteCodeChunk.Save(chunk2, sscPath);
        var sscSize = new FileInfo(sscPath).Length;
        Console.WriteLine($"已保存: {sscPath} ({sscSize} bytes)");
        Console.WriteLine($"指令数: {chunk2.Code.Count}, 常量数: {chunk2.ConstantCount}");

        // 加载
        GlobalSlotRegistry.Reset();
        var loadedChunk = ByteCodeChunk.Load(sscPath);
        Console.WriteLine($"已加载: 指令数={loadedChunk.Code.Count}, 常量数={loadedChunk.ConstantCount}");

        string? result2;
        try
        {
            var task3 = engine.CreateTask(loadedChunk, sscPath);
            var value3 = await task3.RunAsync();
            result2 = value3?.ToString() ?? "null";
            Console.WriteLine($"结果: {result2}");
        }
        catch (Exception ex)
        {
            result2 = $"<异常: {ex.Message}>";
            Console.WriteLine($"异常: {ex}");
        }

        // 3. 对比
        Console.WriteLine("\n--- [3] 对比结果 ---");
        if (result1 == result2)
        {
            Console.WriteLine("✅ 通过！两次执行结果完全一致。");
        }
        else
        {
            Console.WriteLine($"❌ 失败！结果不一致。");
            Console.WriteLine($"  直接执行: {result1}");
            Console.WriteLine($"  加载执行: {result2}");
        }
    }

    // ==================== 保存模式 ====================

    static async Task SaveMode(string scriptPath)
    {
        var exePath = AppDomain.CurrentDomain.BaseDirectory;
        var fullPath = Path.GetFullPath(Path.Combine(exePath, scriptPath));
        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"文件不存在: {fullPath}");
            return;
        }

        var ast = ParseScript(fullPath);
        var compiler = new Compiler();
        var chunk = compiler.Compile(ast);

        string sscPath = Path.ChangeExtension(fullPath, ".ssc");
        ByteCodeChunk.Save(chunk, sscPath);

        var info = new FileInfo(sscPath);
        var sourceInfo = new FileInfo(fullPath);
        Console.WriteLine($"编译完成: {sscPath}");
        Console.WriteLine($"  源码大小: {sourceInfo.Length} bytes");
        Console.WriteLine($"  .ssc 大小: {info.Length} bytes");
        Console.WriteLine($"  压缩率: {(double)info.Length / sourceInfo.Length * 100:F1}%");
        Console.WriteLine($"  指令数: {chunk.Code.Count}");
        Console.WriteLine($"  常量数: {chunk.ConstantCount}");
        if (chunk.VariableTable != null)
        {
            var vt = chunk.VariableTable;
            Console.WriteLine($"  变量表: L={vt.LocalCount} C={vt.CaptureCount} G={vt.GlobalCount} B={vt.BuiltinCount}");
        }
    }

    // ==================== 加载模式 ====================

    static async Task LoadMode(string sscPath)
    {
        var exePath = AppDomain.CurrentDomain.BaseDirectory;
        var fullPath = Path.GetFullPath(Path.Combine(exePath, sscPath));
        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"文件不存在: {fullPath}");
            return;
        }

        Console.WriteLine($"加载 .ssc 文件: {fullPath}");
        var chunk = ByteCodeChunk.Load(fullPath);
        Console.WriteLine($"加载成功: 指令数={chunk.Code.Count}, 常量数={chunk.ConstantCount}");

        GlobalSlotRegistry.Reset();
        var engine = CreateEngine();
        try
        {
            var sw = Stopwatch.StartNew();
            var task = engine.CreateTask(chunk, fullPath);
            var result = await task.RunAsync();
            sw.Stop();
            Console.WriteLine($"结果: {result}");
            Console.WriteLine($"耗时: {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"异常: {ex}");
        }
    }

    // ==================== 批量对比模式 ====================

    static async Task BenchMode(string[][] scripts)
    {
        int total = 0, passed = 0;
        var failures = new List<string>();
        var engine = CreateEngine();

        foreach (var group in scripts)
        {
            foreach (var script in group)
            {
                total++;
                var exePath = AppDomain.CurrentDomain.BaseDirectory;
                var fullPath = Path.GetFullPath(Path.Combine(exePath, script));

                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"⚠ 跳过（文件不存在）: {script}");
                    continue;
                }

                try
                {
                    // 直接执行
                    GlobalSlotRegistry.Reset();
                    engine.ClearCache();
                    var task1 = engine.CreateTask(fullPath);
                    var value1 = await task1.RunAsync();
                    string result1 = value1?.ToString() ?? "null";

                    // 编译→保存→加载→执行
                    GlobalSlotRegistry.Reset();
                    var ast2 = ParseScript(fullPath);
                    var compiler2 = new Compiler();
                    var chunk2 = compiler2.Compile(ast2);

                    string sscPath = Path.ChangeExtension(fullPath, ".ssc");
                    ByteCodeChunk.Save(chunk2, sscPath);

                    GlobalSlotRegistry.Reset();
                    engine.ClearCache();
                    var loadedChunk = ByteCodeChunk.Load(sscPath);
                    var task3 = engine.CreateTask(loadedChunk, sscPath);
                    var value3 = await task3.RunAsync();
                    string result3 = value3?.ToString() ?? "null";

                    if (result1 == result3)
                    {
                        Console.WriteLine($"✅ {Path.GetFileName(script)}");
                        passed++;
                    }
                    else
                    {
                        Console.WriteLine($"❌ {Path.GetFileName(script)}");
                        Console.WriteLine($"   直接执行: {result1}");
                        Console.WriteLine($"   加载执行: {result3}");
                        failures.Add(script);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ {Path.GetFileName(script)}: {ex.Message}");
                }
            }
        }

        Console.WriteLine($"\n========================================");
        Console.WriteLine($"对比结果: {passed}/{total} 通过");
        if (failures.Count > 0)
        {
            Console.WriteLine($"失败列表:");
            foreach (var f in failures)
                Console.WriteLine($"  - {f}");
        }
    }

    // ==================== 辅助方法 ====================

    static ScriptEngine CreateEngine()
    {
        var engine = new ScriptEngine();
        engine.PrototypeManager.Register<TestPersonPrototype>();
        if (!BuiltinFunctions.FunctionCaches.Any(f => f.Name == "new_Person"))
        {
            BuiltinFunctions.FunctionCaches.Add(new FunctionValue("new_Person", _ => new ClrObjectValue(new TestPerson())));
        }
        return engine;
    }

    static ScriptLang.Parser.Expr ParseScript(string fullPath)
    {
        string script = File.ReadAllText(fullPath);
        var lexer = new ScriptLang.Lexer.Lexer(script, fullPath);
        var tokens = lexer.ScanTokens();
        var parser = new ScriptLang.Parser.Parser(tokens, fullPath);
        var expr = parser.Parse();

        if (parser.Diagnostics.Count > 0)
        {
            foreach (var diag in parser.Diagnostics)
                Console.WriteLine($"解析异常: {diag}");
            throw new Exception($"Parser 阶段产生 {parser.Diagnostics.Count} 个异常");
        }

        return expr;
    }
}
