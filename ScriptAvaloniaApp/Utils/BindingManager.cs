using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils
{
    public static class BindingManager
    {
        private static readonly List<Func<Task>> _bindings = new();

        public static void Register(Func<Task> updater)
        {
            _bindings.Add(updater);
        }

        public static async Task RefreshAll()
        {
            var updatersCopy = _bindings.ToList();
            foreach (var updater in updatersCopy)
            {
                await updater();
            }
        }

        public static void Clear()
        {
            _bindings.Clear();
        }
    }

}
