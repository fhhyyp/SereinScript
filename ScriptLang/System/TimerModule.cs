using ScriptLang.Runtime;

namespace ScriptLang.System
{
    /// <summary>
    /// 定时器模块
    /// </summary> 
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class TimerModule : ScriptRuntimeObject<TimerModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is TimerModule;

        /// <summary>
        /// 延迟指定毫秒
        /// </summary>
        /// <param name="ms">延迟毫秒数</param>
        [PrototypeFunction]
        public static async Task<Value> Sleep(NumberValue<int> ms)
        {
            await Task.Delay(ms.Value);
            return Value.Null;
        }

        /// <summary>
        /// 设置延迟执行的定时器
        /// </summary>
        /// <param name="callback">回调函数</param>
        /// <param name="ms">延迟毫秒数</param>
        /// <returns>定时器对象</returns>
        [PrototypeFunction]
        public static ClrObjectValue SetTimeout(FunctionValue callback, NumberValue<int> ms)
        {
            var timer = new Timer(async _ =>
            {
                await callback.CallAsync(null, new List<Value>());
            }, null, ms.Value, Timeout.Infinite);

            return new ClrObjectValue(new TimerWrapper(timer));
        }

        /// <summary>
        /// 设置周期性执行的定时器
        /// </summary>
        /// <param name="callback">回调函数</param>
        /// <param name="ms">间隔毫秒数</param>
        /// <returns>定时器对象</returns>
        [PrototypeFunction]
        public static ClrObjectValue SetInterval(FunctionValue callback, NumberValue<int> ms)
        {
            var timer = new Timer(async _ =>
            {
                await callback.CallAsync(null, new List<Value>());
            }, null, ms.Value, ms.Value);

            return new ClrObjectValue(new TimerWrapper(timer));
        }

        /// <summary>
        /// 清除定时器
        /// </summary>
        /// <param name="timer">定时器对象</param>
        [PrototypeFunction]
        public static void ClearTimer(ClrObjectValue timer)
        {
            if (timer.Value is TimerWrapper wrapper)
            {
                wrapper.Dispose();
            }
        }
    }

    /// <summary>
    /// 定时器包装器
    /// </summary>
    /// <remarks>
    /// 初始化定时器包装器
    /// </remarks>
    /// <param name="timer">定时器对象</param>
    internal class TimerWrapper(Timer timer) : IDisposable
    {
        private Timer? _timer = timer;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}