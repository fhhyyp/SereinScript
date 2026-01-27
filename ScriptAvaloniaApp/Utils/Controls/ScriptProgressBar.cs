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

        public override Control Create()
        {
            var pb = new ProgressBar();
            ApplyAllProperties(pb);

            if (Node.Properties.TryGetValue("Minimum", out var min)) pb.Minimum = min.AsNumber();
            if (Node.Properties.TryGetValue("Maximum", out var max)) pb.Maximum = max.AsNumber();

            if (Node.Properties.TryGetValue("Value", out var val) && val is FunctionValue func)
            {
                async Task Update()
                {
                    var v = await func.CallAsync(new List<Value>(), Engine);
                    pb.Value = v.AsNumber();
                }
                BindingManager.Register(Update);
                _ = Update();
            }

            return pb;
        }
    }

}
