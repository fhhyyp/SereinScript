using ScriptLang.Runtime;

namespace ScriptLang
{

    /// <summary>
    /// 创建一个执行任务，该任务可重复执行
    /// </summary>
    /// <param name="task"></param>
    /// <param name="cts"></param>
    public sealed class ScriptTask(Func<Task<Value>> task, CancellationTokenSource cts)
    {
        private readonly Func<Task<Value>> task = task;

        public CancellationToken Token => cts.Token;

        public bool IsCanceled => Token.IsCancellationRequested;

        public async Task<Value> RunAsync() => await task.Invoke();

        public void Cancel() => cts.Cancel();
    }


}
