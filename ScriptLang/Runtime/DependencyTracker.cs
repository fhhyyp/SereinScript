using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ScriptLang.Runtime
{
    /// <summary>
    /// 依赖收集
    /// </summary>
    public static class DependencyTracker
    {
        [ThreadStatic]
        private static HashSet<ObjectValue>? _deps;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eval"></param>
        /// <returns></returns>
        public static async Task<HashSet<ObjectValue>> CaptureAsync(
            Func<Task<Value>> eval)
        {
            _deps = new HashSet<ObjectValue>();
            await eval();
            var result = _deps;
            _deps = null;
            return result!;
        }

        public static void Track(Value v)
        {
            if (_deps != null && v is ObjectValue obj)
            {
                if (obj.ContainsKey("Watch"))
                    _deps.Add(obj);
            }
        }
    }
}
