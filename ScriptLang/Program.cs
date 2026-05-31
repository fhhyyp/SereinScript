using ScriptLang.Runtime;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ScriptLang;

class Program
{
    static async Task Main(string[] args)
    {
#if DEBUG
        args = ["./Samples/3/3.4-快速排序.script"];
        args = ["./Samples/3/3.5-矩阵运算.script"];
        args = ["./Samples/高级/pinia/run-import.script"];
        args = ["./Samples/高级/linq/run-linq.script"];
#endif
        //args = [".\\Samples\\2\\2.2-条件表达式.script"];
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
            var scriptEngine = new ScriptEngine();
            var sw = Stopwatch.StartNew();
            var result = await scriptEngine.RunAsync(scriptPath);
            sw.Stop();
            Console.WriteLine($"耗时: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"结果: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"异常: {ex}");
        }

    }

}
