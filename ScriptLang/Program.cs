using ScriptLang.Runtime;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ScriptLang;

class Program
{
    static async Task Main(string[] args)
    {
        // 默认运行 demo.script
        var scriptPath = args.Length > 0 ? args[0] : "./Samples/LINQ/run-linq.script";
        
        if (!File.Exists(scriptPath))
        {
            Console.WriteLine($"Error: Script file not found: {scriptPath}");
            return;
        }

        try
        {
            var source = File.ReadAllText(scriptPath);

            ScriptEngine scriptEngine = new ScriptEngine();
          /*  // 1. 词法分析
            var lexerObj = new Lexer.Lexer(source);
            var tokens = lexerObj.ScanTokens();

            // 2. 语法分析
            var parser = new Parser.Parser(filePath: tokens);
            var ast = parser.Parse();

            // 3. 异步执行
            var interpreter = new Interpreter("Samples/LINQ");
            var scope = new Scope(null);
            BuiltinFunctions.RegisterAll(scope);*/

            // 仅在 CLR 测试脚本中注册 CLR 对象
            

            var sw = Stopwatch.StartNew(); // 开始计时
            var result = await scriptEngine.RunAsync(source, scriptPath, (scope) =>
            {
                if (scriptPath.Contains("CLR") || scriptPath.Contains("4.1")
                || scriptPath.Contains("4.2") || scriptPath.Contains("4.3"))
                {
                    var testPerson = new TestPerson
                    {
                        Name = "Alice",
                        Age = 25,
                        Hobbies = new List<string> { "Reading", "Coding", "Gaming" }
                    };
                    scope.Define("person", new ClrObjectValue(testPerson));
                    scope.Define("asyncService", new ClrObjectValue(new TestAsyncService()));
                    //scope.Define("callbackTest", new ClrObjectValue(new CallbackTest()));
                }
                scope.Define("math", new ClrObjectValue(new TestMath()));
            });
            sw.Stop(); // 停止计时
            Console.WriteLine($"耗时: {sw.ElapsedMilliseconds} ms");


            // 4. 输出结果
            Console.WriteLine($"Result: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
    }
}
