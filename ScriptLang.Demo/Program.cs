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
            @".\Samples\2\2.5-错误处理.script",
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
        //scirpt(2, 5);
        void scirpt(int page, int index)
        {
            var script = scripts[page - 1][index - 1];
            args = [script];
        }
         

        if (args.Length == 0)
        {
            Console.WriteLine("用法:");
            Console.WriteLine("  ScriptLang.Demo <script-path>            直接执行脚本");
            Console.WriteLine("  ScriptLang.Demo --compare <script-path>  编译执行 vs 编译→保存→加载→执行 对比");
            Console.WriteLine("  ScriptLang.Demo --save <script-path>     编译并保存为 .ssc 文件");
            Console.WriteLine("  ScriptLang.Demo --load <ssc-path>        加载 .ssc 文件并执行");
            Console.WriteLine("  ScriptLang.Demo --build <script-path>    递归编译脚本及其所有 import 依赖为 .ssc");
            //Console.WriteLine("  ScriptLang.Demo --bench                  批量对比所有预设脚本");
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

        if (args[0] == "--build" && args.Length >= 2)
        {
            BuildMode(args[1]);
            return;
        }

        // 默认模式：直接执行
        await RunScript(args[0]);

        
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

    // ==================== 构建模式：递归批量编译 ====================

    /// <summary>
    /// 递归编译脚本及其所有 import 依赖为 .ssc
    /// 依赖按拓扑顺序编译（叶子先编译），支持循环依赖（通过已编译集合去重）
    /// </summary>
    static void BuildMode(string scriptPath)
    {
        var exePath = AppDomain.CurrentDomain.BaseDirectory;
        var fullPath = Path.GetFullPath(Path.Combine(exePath, scriptPath));
        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"文件不存在: {fullPath}");
            return;
        }

        var compiled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootDir = Path.GetDirectoryName(fullPath)!;

        Console.WriteLine($"递归编译: {Path.GetFileName(fullPath)}");
        Console.WriteLine($"根目录: {rootDir}");
        Console.WriteLine();

        BuildRecursive(fullPath, rootDir, compiled);

        Console.WriteLine($"\n完成：共编译 {compiled.Count} 个文件");
    }

    /// <summary>递归编译脚本及其依赖</summary>
    static void BuildRecursive(string scriptPath, string rootDir, HashSet<string> compiled)
    {
        // 规范化路径避免重复
        var normalizedPath = Path.GetFullPath(scriptPath);

        string sscPath = Path.ChangeExtension(normalizedPath, ".ssc");

        if (compiled.Contains(sscPath))
            return;

        // 解析脚本收集 import 依赖
        var ast = ParseScript(normalizedPath);
        var imports = CollectImports(ast);

        // 先编译依赖（深度优先 → 叶子先编译）
        foreach (var importPath in imports)
        {
            var resolvedPath = ResolveImportForBuild(importPath, rootDir);
            if (resolvedPath != null && File.Exists(resolvedPath))
            {
                BuildRecursive(resolvedPath, rootDir, compiled);
            }
            else
            {
                Console.WriteLine($"  ⚠ 跳过（找不到源文件）: {importPath}");
            }
        }

        // 编译当前脚本
        var compiler = new Compiler();
        var chunk = compiler.Compile(ast);

        ByteCodeChunk.Save(chunk, sscPath);
        compiled.Add(sscPath);

        var info = new FileInfo(sscPath);
        var sourceInfo = new FileInfo(normalizedPath);
        Console.WriteLine($"  ✅ {Path.GetFileName(normalizedPath)} → {Path.GetFileName(sscPath)}  ({info.Length} bytes, {chunk.Code.Count} 指令)");
    }

    /// <summary>从 AST 收集中所有 import 的文件路径</summary>
    static List<string> CollectImports(ScriptLang.Parser.Expr ast)
    {
        var imports = new List<string>();

        CollectImportsRecursive(ast, imports);

        return imports;
    }

    static void CollectImportsRecursive(ScriptLang.Parser.Expr expr, List<string> imports)
    {
        switch (expr)
        {
            case ScriptLang.Parser.ImportStmt import:
                imports.Add(import.FilePath);
                break;

            case ScriptLang.Parser.ProgramExpr program:
                foreach (var stmt in program.Statements)
                    CollectImportsRecursive(stmt, imports);
                break;

            case ScriptLang.Parser.BlockExpr block:
                foreach (var stmt in block.Statements)
                    CollectImportsRecursive(stmt, imports);
                break;

            case ScriptLang.Parser.LambdaExpr lambda:
                CollectImportsRecursive(lambda.Body, imports);
                break;

            case ScriptLang.Parser.IfExpr ifExpr:
                CollectImportsRecursive(ifExpr.Then, imports);
                CollectImportsRecursive(ifExpr.Else, imports);
                break;

            case ScriptLang.Parser.WhenExpr whenExpr:
                foreach (var clause in whenExpr.Clauses)
                    CollectImportsRecursive(clause.Body, imports);
                if (whenExpr.OtherClause != null)
                    CollectImportsRecursive(whenExpr.OtherClause.Body, imports);
                break;

            case ScriptLang.Parser.ForExpr forExpr:
                CollectImportsRecursive(forExpr.Body, imports);
                break;

            case ScriptLang.Parser.ReturnExpr ret when ret.Value != null:
                CollectImportsRecursive(ret.Value, imports);
                break;

            case ScriptLang.Parser.BinaryExpr binary:
                CollectImportsRecursive(binary.Left, imports);
                CollectImportsRecursive(binary.Right, imports);
                break;

            case ScriptLang.Parser.UnaryExpr unary:
                CollectImportsRecursive(unary.Expr, imports);
                break;

            case ScriptLang.Parser.ConditionalExpr cond:
                CollectImportsRecursive(cond.Cond, imports);
                CollectImportsRecursive(cond.Then, imports);
                CollectImportsRecursive(cond.Else, imports);
                break;

            case ScriptLang.Parser.CallExpr call:
                CollectImportsRecursive(call.Target, imports);
                foreach (var arg in call.Args)
                    CollectImportsRecursive(arg, imports);
                break;

            case ScriptLang.Parser.LetExpr let:
                CollectImportsRecursive(let.Value, imports);
                break;

            case ScriptLang.Parser.VarExpr var:
                CollectImportsRecursive(var.Value, imports);
                break;

            case ScriptLang.Parser.AssignExpr assign:
                CollectImportsRecursive(assign.Value, imports);
                break;

            case ScriptLang.Parser.ArrayLiteralExpr arr:
                foreach (var elem in arr.Elements)
                    CollectImportsRecursive(elem, imports);
                break;

            case ScriptLang.Parser.ObjectLiteralExpr obj:
                foreach (var prop in obj.Properties)
                    CollectImportsRecursive(prop.Value, imports);
                break;

            case ScriptLang.Parser.MemberAccessExpr member:
                CollectImportsRecursive(member.Target, imports);
                break;

            case ScriptLang.Parser.MemberAssignExpr memberAssign:
                CollectImportsRecursive(memberAssign.Target, imports);
                CollectImportsRecursive(memberAssign.Value, imports);
                break;

            case ScriptLang.Parser.IndexAccessExpr index:
                CollectImportsRecursive(index.Target, imports);
                CollectImportsRecursive(index.Index, imports);
                break;

            case ScriptLang.Parser.IndexAssignExpr indexAssign:
                CollectImportsRecursive(indexAssign.Target, imports);
                CollectImportsRecursive(indexAssign.Index, imports);
                CollectImportsRecursive(indexAssign.Value, imports);
                break;
        }
    }

    /// <summary>为构建模式解析 import 路径到实际 .script 文件</summary>
    static string? ResolveImportForBuild(string importPath, string rootDir)
    {
        // 如果是相对路径，基于 rootDir 解析
        string basePath;
        if (Path.IsPathRooted(importPath))
            basePath = Path.GetFullPath(importPath);
        else
            basePath = Path.GetFullPath(Path.Combine(rootDir, importPath));

        // 已有后缀 → 直接返回（ImportResolver 运行时会在 .ssc/.script 间选择）
        if (Path.HasExtension(basePath))
        {
            // 构建阶段我们编译 .script 源文件
            string scriptPath = basePath.EndsWith(".ssc", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(basePath, ".script")
                : basePath;
            return scriptPath;
        }

        // 无后缀 → 找 .script（构建时编译源文件）
        string withScript = basePath + ".script";
        if (File.Exists(withScript)) return withScript;

        // 如果 .ssc 已存在（无需重新编译），返回 null
        string withSsc = basePath + ".ssc";
        if (File.Exists(withSsc)) return null;

        return null;
    }

    // ==================== 加载模式 ====================

    static async Task LoadMode(string sscPath)
    {
        var exePath = AppDomain.CurrentDomain.BaseDirectory;
        var fullPath = Path.GetFullPath(Path.Combine(exePath, sscPath));
        if (!File.Exists(fullPath))
        {
            Console.Error.WriteLine($"文件不存在: {fullPath}");
            return;
        }

        //Console.WriteLine($"加载 .ssc 文件: {fullPath}");
        var chunk = ByteCodeChunk.Load(fullPath);
        //Console.WriteLine($"加载成功: 指令数={chunk.Code.Count}, 常量数={chunk.ConstantCount}");

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
            Console.Error.WriteLine($"异常: {ex}");
        }
    }

    
    // ==================== 辅助方法 ====================

    static ScriptEngine CreateEngine()
    {
        var engine = new ScriptEngine();
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

}
