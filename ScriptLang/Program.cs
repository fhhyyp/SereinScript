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
        args = ["./Samples/1/1.1-基础运算.script"];
        //args = [".\\Samples\\2\\2.2-条件表达式.script"];
        args = [@"D:\Project\C#\SereinScript\SereinScript\ScriptLang\Samples\3\3.5-矩阵运算.script"];
        args = ["D:\\Project\\C#\\SereinScript\\SereinScript\\ScriptLang\\Samples\\test\\test-stack-overflow.script"];
        args = ["./Samples/3/3.4-快速排序.script"];
#endif

        args = [@"D:\Project\C#\SereinScript\SereinScript\ScriptLang\Samples\1\1.7-数值数据类型.script"];
        args = [@"D:\Project\C#\SereinScript\SereinScript\ScriptLang\Samples\test\test.script"];
        args = ["./Samples/高级/pinia/run-import.script"];
        args = ["./Samples/高级/linq/run-linq.script"];
        args = [".\\Samples\\test\\test_closure_memory.script"];


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

        try
        {
            var engine = new ScriptEngine();
            if(1 ==11)
            {

             engine.Mode = ExecutionMode.Interpreted;
            }
            var sw = Stopwatch.StartNew();
            var task = engine.CreateTask(scriptPath);
            var result = await task; //.RunAsync();
            sw.Stop();
            //Console.WriteLine($"耗时: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"结果: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"异常: {ex}");
        }


        while (true)
        {
           
            //Console.WriteLine($"__");
            await Task.Delay(5000);
            //break;
        }

    }

}
