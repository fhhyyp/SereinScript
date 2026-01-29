using Avalonia.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public class ScriptCheckBox : ScriptControlBase
    {
        public ScriptCheckBox(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override async Task<Control> CreateAsync()
        {
            var cb = new CheckBox();

            IgnoreProperties.Add(nameof(cb.IsChecked));

            await ApplyAllPropertiesAsync(cb);
            if (Node.TryGetValue(nameof(cb.IsChecked), out var val) 
                && TryGetObjectValue(val, out var sourceObject, out var targetKey))
            {
                async Task Refresh()
                {
                    await Task.CompletedTask;
                    var value = sourceObject.Get(targetKey).AsBool();
                    cb.IsChecked = value;
                }
               await SubObjectChangedAsync(sourceObject, targetKey, Refresh);
                cb.IsCheckedChanged += (_, _) =>
                {
                    sourceObject.Set(targetKey, new BoolValue(cb.IsChecked ?? false));
                };
            }
            return cb;
        }
    }

}
