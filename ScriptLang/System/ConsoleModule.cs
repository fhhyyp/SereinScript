using System.Diagnostics;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    /// <summary>
    /// 控制台模块
    /// </summary>
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class ConsoleModule : ScriptRuntimeObject<ConsoleModule>
    {
        private static readonly Dictionary<string, Stopwatch> _stopwatches = new();

        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is ConsoleModule;

        /// <summary>
        /// 输出日志信息
        /// </summary>
        /// <param name="value">输出的值</param>
        [PrototypeFunction]
        public static void Log(Value value)
            => Console.WriteLine(value?.ToString() ?? "null");

        /// <summary>
        /// 输出错误信息
        /// </summary>
        /// <param name="value">输出的值</param>
        [PrototypeFunction]
        public static void Error(Value value)
            => Console.Error.WriteLine(value?.ToString() ?? "null");

        /// <summary>
        /// 从控制台读取一行输入
        /// </summary>
        /// <returns>读取的字符串</returns>
        [PrototypeFunction]
        public static async Task<StringValue> ReadLine()
        {
            var line = await Task.Run(() => Console.ReadLine());
            return StringValue.Create(line ?? "");
        }

        /// <summary>
        /// 清空控制台
        /// </summary>
        [PrototypeFunction]
        public static void Clear()
            => Console.Clear();

        /// <summary>
        /// 开始计时
        /// </summary>
        /// <param name="label">计时器标签</param>
        [PrototypeFunction]
        public static void Time(StringValue label)
        {
            _stopwatches[label.Value] = Stopwatch.StartNew();
        }

        /// <summary>
        /// 结束计时并输出耗时
        /// </summary>
        /// <param name="label">计时器标签</param>
        [PrototypeFunction]
        public static void TimeEnd(StringValue label)
        {
            if (_stopwatches.TryGetValue(label.Value, out var sw))
            {
                sw.Stop();
                _stopwatches.Remove(label.Value);
                Console.WriteLine($"{label.Value}: {sw.ElapsedMilliseconds}ms");
            }
        }

        /// <summary>
        /// 控制台颜色常量
        /// </summary>
        [PrototypeProperty]
        private static ObjectValue Colors()
        {
            return new ObjectValue(new Dictionary<string, Value>
            {
                ["reset"] = StringValue.Create("\x1b[0m"),
                ["red"] = StringValue.Create("\x1b[31m"),
                ["green"] = StringValue.Create("\x1b[32m"),
                ["yellow"] = StringValue.Create("\x1b[33m"),
                ["blue"] = StringValue.Create("\x1b[34m"),
                ["magenta"] = StringValue.Create("\x1b[35m"),
                ["cyan"] = StringValue.Create("\x1b[36m"),
                ["white"] = StringValue.Create("\x1b[37m")
            });
        }
    }
}