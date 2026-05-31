using Avalonia.Controls;
using Avalonia.Interactivity;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    internal class ScriptScrollViewer : ScriptControlBase
    {
        public ScriptScrollViewer(ObjectValue node, ScriptEngine interpreter) : base(node, interpreter) { }

        public async override Task<Control> CreateAsync()
        {
            var view = new ScrollViewer();

            IgnoreProperties.Add(nameof(ScrollViewer.Content));
            await ApplyAllPropertiesAsync(view);
            foreach (var (k, v) in Node.Properties)
            {
                if(k != nameof(ScrollViewer.Content) || v is not ObjectValue child)
                {
                    continue;
                }

                foreach (var (key, value) in child.Properties)
                {
                    if (value is ObjectValue c && ScriptControlFactory.IsControlType(key))
                    {
                        var ctrl = await ScriptControlFactory.CreateAsync(key, c, Engine);
                        view.Content = ctrl;
                        break;
                    }
                }
            }
            return view;
        }
    }
}
