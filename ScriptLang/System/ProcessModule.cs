using System.Collections;
using System.Diagnostics;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    /// <summary>
    /// 进程模块
    /// </summary>   
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class ProcessModule : ScriptRuntimeObject<ProcessModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is ProcessModule;

        /// <summary>
        /// 命令行参数
        /// </summary>
        [PrototypeProperty]
        private static ObjectValue Argv()
        {
            var args = Environment.GetCommandLineArgs();
            var list = args.Skip(1).Select(a => (Value)StringValue.Create(a)).ToList();
            return new ObjectValue(new Dictionary<string, Value>
            {
                ["args"] = new ArrayValue(list),
                ["execPath"] = StringValue.Create(args[0])
            });
        }

        /// <summary>
        /// 进程 ID
        /// </summary>
        [PrototypeProperty]
        private static NumberValue<int> Pid()
            => NumberValueFactory.Create(Environment.ProcessId);

        /// <summary>
        /// 当前工作目录
        /// </summary>
        [PrototypeProperty]
        private static StringValue Cwd()
            => StringValue.Create(Directory.GetCurrentDirectory());

        /// <summary>
        /// 更改工作目录
        /// </summary>
        /// <param name="path">目标目录</param>
        [PrototypeFunction]
        public static void ChDir(StringValue path)
            => Directory.SetCurrentDirectory(path.Value);

        /// <summary>
        /// 环境变量
        /// </summary>
        /// <returns>环境变量对象</returns>
        [PrototypeFunction]
        public static ObjectValue Env()
        {
            var env = Environment.GetEnvironmentVariables();
            var dict = new Dictionary<string, Value>();
            foreach (DictionaryEntry entry in env)
            {
                dict[entry.Key.ToString()] = StringValue.Create(entry.Value?.ToString() ?? "");
            }
            return new ObjectValue(dict);
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="command">要执行的命令</param>
        /// <returns>退出代码</returns>
        [PrototypeFunction]
        public static async Task<NumberValue<int>> Execute(StringValue command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = command.Value,
                UseShellExecute = true
            };

            var process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return NumberValueFactory.Create(process.ExitCode);
            }

            return NumberValueFactory.Create(-1);
        }

        /// <summary>
        /// 退出进程
        /// </summary>
        /// <param name="code">退出代码（默认 0）</param>
        [PrototypeFunction]
        public static void Exit(NumberValue<int> code) => Environment.Exit(code?.Value ?? 0);

        /// <summary>
        /// 获取进程运行时间
        /// </summary>
        [PrototypeProperty]
        private static NumberValue<double> Uptime() => NumberValueFactory.Create(Environment.TickCount64 / 1000.0);
    }
}