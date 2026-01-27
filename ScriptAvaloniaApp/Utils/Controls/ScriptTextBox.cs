using Avalonia.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{


    public class ScriptTextBox : ScriptControlBase
    {
        public ScriptTextBox(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override Control Create()
        {
            var tb = new TextBox();

            ApplyAllProperties(tb);

            // getter
            if (Node.Properties.TryGetValue("Text", out var textVal) && textVal is FunctionValue getter)
            {
                async Task Update()
                {
                    var v = await getter.CallAsync(new List<Value>(), Engine);
                    tb.Text = v.AsString();
                }
                BindingManager.Register(Update);
                _ = Update();
            }

            // setter
            if (Node.Properties.TryGetValue("OnInput", out var inputVal) && inputVal is FunctionValue setter)
            {
                tb.TextChanged += async (_, __) =>
                {
                    await setter.CallAsync(new List<Value> { new StringValue(tb.Text ?? "") }, Engine);
                    await BindingManager.RefreshAll();
                };
            }

            return tb;
        }
    }


}
