using Avalonia.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public class ScriptProgressBar : ScriptControlBase
    {
        public ScriptProgressBar(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override async Task<Control> CreateAsync()
        {
            var pb = new ProgressBar();
            await ApplyAllPropertiesAsync(pb);

            // if (Node.TryGetValue("Minimum", out var min)) pb.Minimum = min.AsNumber();
            // if (Node.TryGetValue("Maximum", out var max)) pb.Maximum = max.AsNumber();

            /*if (Node.TryGetValue("Value", out var val) && val.Value is FunctionValue func)
            {
                async Task Update()
                {
                    var v = await func.CallAsync(Engine);
                    pb.Value = v.AsNumber();
                }
                await BindingManager.Register(Update);
            }*/

            return pb;
        }
    }

}
