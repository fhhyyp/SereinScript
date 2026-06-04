using ScriptLang.Runtime;

namespace ScriptLang
{
    public sealed class ScriptTask(Task<Value> task, CancellationTokenSource cts)
    {
        private readonly Task<Value> task = task;

        public CancellationToken Token => cts.Token;

        public bool IsCanceled => Token.IsCancellationRequested;

        public async Task<Value> RunAsync() => await task;

        public void Cancel() => cts.Cancel();
    }


}
