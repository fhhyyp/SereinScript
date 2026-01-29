using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils
{
    public static class ScriptUIBuilder
    {
        public static async Task<Control> BuildUIAsync(Value uiConfig, ScriptEngine interpreter)
        {
            if (uiConfig is not ObjectValue root)
                throw new Exception("UI root must be ObjectValue");

            var first = root.Properties.First();
            var control = await ScriptControlFactory.CreateAsync(first.Key, (ObjectValue)first.Value, interpreter);
          
            return control;
        }

        public static void UIInvoke<T>(this T control, Action<T> action) where T : Control
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                try
                {
                    action.Invoke(control);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            });
        }
    }


}
