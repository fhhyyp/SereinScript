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

        public override Control Create()
        {
            var cb = new CheckBox();
            ApplyAllProperties(cb);

            // 绑定 IsChecked
            if (Node.Properties.TryGetValue("IsChecked", out var val) && val is FunctionValue func)
            {
                async Task Update()
                {
                    var v = await func.CallAsync(new List<Value>(), Engine);
                    cb.IsChecked = v.AsBool();
                }
                BindingManager.Register(Update);
                _ = Update();
            }

            // 双向绑定（OnChecked / OnUnchecked）
            if (Node.Properties.TryGetValue("OnInput", out var setterVal) && setterVal is FunctionValue setter)
            {
                cb.IsCheckedChanged += async (sender, __) =>
                {
                    if(sender is not CheckBox checkBox)
                    {
                        return;
                    }
                    var state = checkBox?.IsChecked ?? false;
                    await setter.CallAsync(new List<Value> { new BoolValue(state) }, Engine);
                    await BindingManager.RefreshAll();
                };
            }

            return cb;
        }
    }

}
