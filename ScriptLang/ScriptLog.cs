using System.Text;
using SystemDebug = System.Diagnostics.Debug;

namespace ScriptLang;

/// <summary>
/// 脚本日志静态类
/// 统一管理编译时、运行时和环境异常的日志输出
/// </summary>
public static class ScriptLog
{
    private static readonly object _lock = new();

    /// <summary>
    /// 日志文件路径
    /// </summary>
    public static string LogFilePath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "logs", "script.log");

    /// <summary>
    /// 是否写入日志文件
    /// </summary>
    public static bool IsWriteLogFile { get; set; } = false;

    /// <summary>
    /// 是否启用 Debug 输出到 System.Diagnostics.Debug
    /// </summary>
    public static bool IsPrintOnDebug { get; set; } = false;

    public static bool IsPrint { get; set; } = false;


    public static void Debug(string message)
    {
        Write("DEBUG", message);

        if(IsPrint)
            Console.WriteLine(message);

        if (IsPrintOnDebug)
            SystemDebug.WriteLine(message);
    }

    public static void Info(string message)
    {
        Write("INFO", message);

        if (IsPrint)
            Console.WriteLine(message);

        if (IsPrintOnDebug)
            SystemDebug.WriteLine(message);
    }

    public static void Error(string message)
    {
        Write("ERROR", message);

        Console.Error.WriteLine(message);

        if (IsPrintOnDebug)
            SystemDebug.WriteLine(message);
    }

    public static void Error(Exception exception)
    {
        var message = exception.ToString();

        Write("ERROR", message);

        Console.Error.WriteLine(message);

        if (IsPrintOnDebug)
            SystemDebug.WriteLine(message);
    }

    private static void Write(string level, string message)
    {
        if (!IsWriteLogFile)
            return;

        try
        {
            var dir = Path.GetDirectoryName(LogFilePath);

            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var line =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";

            lock (_lock)
            {
                File.AppendAllText(
                    LogFilePath,
                    line,
                    Encoding.UTF8);
            }
        }
        catch
        {
            // 日志系统自身异常不能影响业务运行
        }
    }
}