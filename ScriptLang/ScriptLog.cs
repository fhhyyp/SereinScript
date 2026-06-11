using System.Diagnostics;

namespace ScriptLang;

/// <summary>
/// 脚本日志静态类
/// 统一管理编译时、运行时和环境异常的日志输出
/// </summary>
public static class ScriptLog
{
    /// <summary>
    /// 是否启用 Debug 输出到 System.Diagnostics.Debug（方便 VS 调试）
    /// </summary>
    public static bool IsPrintOnDebug { get; set; } = false;

    /// <summary>
    /// 编译时与 VM 运行时指令级操作信息
    /// </summary>
    public static void Debug(string message)
    {
        Console.WriteLine(message);
        if (IsPrintOnDebug)
            global::System.Diagnostics.Debug.WriteLine(message);
    }

    /// <summary>
    /// 必要的基础信息（暂不调用，保留用于后续与其他程序交互时的纯净输出）
    /// </summary>
    public static void Info(string message)
    {
        Console.WriteLine(message);
        if (IsPrintOnDebug)
            global::System.Diagnostics.Debug.WriteLine(message);
    }

    /// <summary>
    /// 语法解析异常、编译异常、运行异常、环境异常
    /// </summary>
    public static void Error(string message)
    {
        Console.Error.WriteLine(message);
        if (IsPrintOnDebug)
            global::System.Diagnostics.Debug.WriteLine(message);
    }

    /// <summary>
    /// 语法解析异常、编译异常、运行异常、环境异常（异常对象形式）
    /// </summary>
    public static void Error(Exception exception)
    {
        var message = exception.ToString();
        Console.Error.WriteLine(message);
        if (IsPrintOnDebug)
            global::System.Diagnostics.Debug.WriteLine(message);
    }
}
