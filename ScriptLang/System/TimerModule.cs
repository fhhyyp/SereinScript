using ScriptLang.Runtime;

namespace ScriptLang.System
{
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class TimerModule : ScriptRuntimeObject<TimerModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is TimerModule;

        [PrototypeFunction]
        [LspDoc("异步延迟指定毫秒数")]
        public static async Task<Value> Sleep(NumberValue<int> ms) { await Task.Delay(ms.Value); return Value.Null; }

        [PrototypeFunction]
        [LspDoc("延迟指定毫秒后执行一次回调")]
        public static ClrObjectValue SetTimeout(ICallable callback, NumberValue<int> ms, ScriptEngine engine)
        { 
            var timer = new Timer(async _ => { 
                await callback.CallAsync(engine); 
            }, null, ms.Value, Timeout.Infinite); 
            return new ClrObjectValue(new TimerWrapper(timer)); }

        [PrototypeFunction]
        [LspDoc("每隔指定毫秒重复执行回调\n返回定时器对象，可用 clearTimer 停止")]
        public static ClrObjectValue SetInterval(ICallable callback, NumberValue<int> ms, ScriptEngine engine)
        { 
            var timer = new Timer(async _ => { 
                await callback.CallAsync(engine); 
            }, null, ms.Value, ms.Value);
            return new ClrObjectValue(new TimerWrapper(timer)); }

        [PrototypeFunction] 
        [LspDoc("清除由 setTimeout/setInterval 创建的定时器")]
        public static void ClearTimer(ClrObjectValue timer) 
        { 
            if (timer.Value is TimerWrapper w) w.Dispose();
        }
    }

    internal class TimerWrapper(Timer timer) : IDisposable
    {
        private Timer? _timer = timer; 
        public void Dispose() 
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
