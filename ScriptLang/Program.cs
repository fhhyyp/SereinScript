using ScriptLang.Prototype;
using ScriptLang.Runtime;
using System.Diagnostics;
using System.Net.Quic;
using System.Threading.Tasks;

namespace ScriptLang;

class Program
{

    static async Task Main(string[] args)
    {
#if DEBUG
        args = ["./Samples/3/3.5-矩阵运算.script"];
        args = ["./Samples/3/3.2-高阶函数.script"];
        //args = ["./Samples/2/2.2-条件表达式.script"];
        args = ["./Samples/test/test-stack-overflow.script"];
        args = ["./Samples/3/3.4-快速排序.script"];

        args = ["./Samples/1/1.7-数值数据类型.script"];
        args = ["./Samples/test/test.script"];
        args = [@"D:\Project\C#\SereinScript\SereinScript\ScriptLang\Samples\2\2.3-循环.script"];
        args = ["./Samples/test/test-mutual-recursion.script"];
                args = ["./Samples/3/3.5-矩阵运算.script"];
        args = ["D:\\Project\\C#\\SereinScript\\SereinScript\\ScriptLang\\Samples\\test\\test.script"];
        args = ["./Samples/高级/pinia/run-import.script"];
        args = ["./Samples/test/test_closure_memory.script"];
        args = ["./Samples/高级/linq/run-linq.script"];
        args = ["./Samples/1/1.1-基础运算.script"];
        args = ["./Samples/4/4.1-CLR对象.script"];
#endif

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
          /* 1 */ @".\Samples\test\test.script",
          /* 2 */ @".\Samples\test\test_closure_memory.script",
          /* 3 */ @".\Samples\test\test-closure-recursion.script",
          /* 4 */ @".\Samples\test\test-deep-recursion.script",
          /* 5 */ @".\Samples\test\test-extreme-recursion.script",
          /* 6 */ @".\Samples\test\test-mutual-recursion.script",
          /* 7 */ @".\Samples\test\test-stack-overflow.script",
        ],
        [ // 6
            @".\Samples\高级\linq\run-linq.script",
            @".\Samples\高级\pinia\run-import.script",
        ],
        ];
        
        scirpt(6, 2);

        if (args.Length == 0)
        {
            Console.WriteLine("需要指定调用的 script 文件路径");
            return;
        }
        var exePath = AppDomain.CurrentDomain.BaseDirectory;
        var scriptPath = Path.GetFullPath( Path.Combine(exePath, args[0]));
        if (!File.Exists(scriptPath))
        {
            Console.WriteLine($"文件不存在: {scriptPath}");
            return;
        }
        var engine = new ScriptEngine();
        //engine.PrototypeManager.Register<TestPersonPrototype>();
        BuiltinFunctions.FunctionCaches.Add(new FunctionValue("new_Person", _ => new ClrObjectValue(new TestPerson())));
        try
        {
            var sw = Stopwatch.StartNew();
            var task = engine.CreateTask(scriptPath);
            var result = await task.RunAsync(); //.RunAsync();
            sw.Stop();
            //Console.WriteLine($"耗时: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"结果: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"异常: {ex}");
        }
        void scirpt(int page, int index) 
        {
            var script = scripts[page - 1][index - 1];
            args = [script];
        }

    }

}
