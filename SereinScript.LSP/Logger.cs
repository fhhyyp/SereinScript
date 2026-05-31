using System;
using System.IO;
using System.Text;
using System.Threading;

namespace SereinScript.LSP
{
    /// <summary>
    /// 日志管理类
    /// </summary>
    public static class Logger
    {
        private static readonly string LogDirectory;
        private static readonly string LogFilePath;
        private static readonly object _lock = new object();

        static Logger()
        {
            // 获取本地临时文件夹
            string tempPath = Path.GetTempPath();
            // 创建SereinScript日志目录
            LogDirectory = Path.Combine(tempPath, "SereinScript", "Logs");
            // 确保目录存在
            Directory.CreateDirectory(LogDirectory);
            // 生成日志文件路径，按日期命名
            string fileName = $"sereinscript-lsp-{DateTime.Now:yyyy-MM-dd}.log";
            LogFilePath = Path.Combine(LogDirectory, fileName);

            // 写入启动日志
            Info("Logger initialized", "Logger");
            Info($"Log directory: {LogDirectory}", "Logger");
            Info($"Log file: {LogFilePath}", "Logger");
        }

        /// <summary>
        /// 写入信息级别日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志类别</param>
        public static void Info(string message, string category = "General")
        {
            WriteLog("INFO", category, message);
        }

        /// <summary>
        /// 写入警告级别日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志类别</param>
        public static void Warning(string message, string category = "General")
        {
            WriteLog("WARNING", category, message);
        }

        /// <summary>
        /// 写入错误级别日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志类别</param>
        public static void Error(string message, string category = "General")
        {
            WriteLog("ERROR", category, message);
        }

        /// <summary>
        /// 写入调试级别日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志类别</param>
        public static void Debug(string message, string category = "General")
        {
            WriteLog("DEBUG", category, message);
        }

        /// <summary>
        /// 写入异常日志
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="category">日志类别</param>
        public static void Exception(Exception ex, string category = "General")
        {
            WriteLog("ERROR", category, $"Exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
        }

        /// <summary>
        /// 实际写入日志的方法
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="category">日志类别</param>
        /// <param name="message">日志消息</param>
        private static void WriteLog(string level, string category, string message)
        {
            try
            {
                lock (_lock)
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{category}] {message}";
                    // 追加写入日志文件
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine, Encoding.UTF8);
                    // 同时输出到控制台，方便调试
                    Console.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                // 如果写入日志失败，输出到控制台
                Console.WriteLine($"Failed to write log: {ex.Message}");
                Console.WriteLine($"Original log: [{level}] [{category}] {message}");
            }
        }

        /// <summary>
        /// 获取日志文件路径
        /// </summary>
        /// <returns>日志文件路径</returns>
        public static string GetLogFilePath()
        {
            return LogFilePath;
        }

        /// <summary>
        /// 获取日志目录路径
        /// </summary>
        /// <returns>日志目录路径</returns>
        public static string GetLogDirectory()
        {
            return LogDirectory;
        }
    }
}
