using Avalonia.Controls;
using Avalonia.Styling;
using ScriptLang;
using ScriptLang.Runtime;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{


    public class ScriptTextBox : ScriptControlBase
    {
        public ScriptTextBox(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override async Task<Control> CreateAsync()
        {
            var tb = new TextBox();

            await ApplyAllPropertiesAsync(tb);
            bool isload = true;
            // getter
            if (Node.TryGetValue("Text", out var value))
            {
                if(TryGetObjectValue(value, out var sourceObject, out var targetKey))
                {
                    async Task UpdateText()
                    {
                        await Task.CompletedTask;
                        if(sourceObject.TryGetValue(targetKey, out var result))
                        {
                            var content = result.AsString();
                            if (tb.Text != content) tb.UIInvoke(c => c.Text = content);
                        }
                    }
                    await SubObjectChangedAsync(sourceObject, targetKey, UpdateText);
                    tb.TextChanged += (sender, e) =>
                    {
                        sourceObject.Set(targetKey, new StringValue(tb.Text ?? string.Empty));
                    };
                }
                else if(TryGetArrayValue(value, out var arrayValue, out var targetIndex))
                {
                    async Task UpdateText()
                    {
                        if (targetIndex >= arrayValue.Length) return;
                        await Task.CompletedTask;
                        var item = arrayValue.Get(targetIndex);
                        var content = item.AsString();
                        if (tb.Text != content) tb.UIInvoke(c => c.Text = content);
                    }
                    await SubArrayChangedAsync(arrayValue, targetIndex, UpdateText);
                    tb.TextChanged += (sender, e) =>
                    {
                        if (targetIndex >= arrayValue.Length) return;
                        var value = new StringValue(tb.Text ?? string.Empty);
                        arrayValue.Set(targetIndex, value, Engine);
                    };
                }
                else
                {
                    tb.Text = value.AsString();
                }
            }

            isload = false;
            return tb;
        }
    }


}
