using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace ScriptLang.Runtime
{
    /*public static class BindingScope
    {
        /// <summary>
        /// 
        /// </summary>
        [ThreadStatic]
        public static BindingExecution? Current;
    }

    public sealed class BindingExecution
    {
        public Func<Task> Update;
        public readonly HashSet<IObservableValue> Dependencies = new();

        public BindingExecution(Func<Task> update)
        {
            Update = update;
        }
    }


    public static class BindingManager
    {
        private static readonly Dictionary<IObservableValue, List<BindingExecution>> _map = new();

        private static readonly SemaphoreSlim _slim = new SemaphoreSlim(1, 1);

        public static async Task Register(Func<Task> update)
        {
            var exec = new BindingExecution(update);
            async Task Wrapped()
            {
                // 清理旧依赖
                await _slim.WaitAsync();
                foreach (var list in _map.Values)
                    list.Remove(exec);


                exec.Dependencies.Clear();
                BindingScope.Current = exec;
                try
                {
                    await update();
                }
                catch(Exception ex)
                {
                    Debug.WriteLine(ex);
                }
                finally
                {
                    BindingScope.Current = null;

                    _slim.Release();
                }

                foreach (var dep in exec.Dependencies)
                {
                    if (!_map.TryGetValue(dep, out var list))
                        _map[dep] = list = new List<BindingExecution>();

                    list.Add(exec);
                }
            }

            exec.Update = Wrapped;
            _ =  Wrapped();
        }

        public static void Notify(IObservableValue value)
        {

            var execs = _map.Where(kvp => kvp.Key == value).SelectMany(x => x.Value).ToArray();
            foreach (var exec in execs)
                _ = exec.Update();
        }

        public static void Clear()
        {
            _map.Clear();
        }
    }
*/


}
