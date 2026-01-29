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

            IgnoreProperties.Add("Text");
            await ApplyAllPropertiesAsync(tb);
            bool isload = true;
            // getter
            if (Node.TryGetValue("Text", out var textVal))
            {
                if(TryGetObjectValue(textVal, out var sourceObject, out var targetKey))
                {
                    async Task UpdateText()
                    {
                        await Task.CompletedTask;
                        var v = sourceObject.Get(targetKey).Value.AsString();
                        if (tb.Text != v)
                        {
                            tb.UIInvoke(c => c.Text = v);
                        }
                    }
                    await SubObjectChangedAsync(sourceObject, targetKey, UpdateText);
                    tb.TextChanged += (sender, e) =>
                    {
                        if (!isload)
                            sourceObject.Set(targetKey, new StringValue(tb.Text ?? string.Empty));
                    };
                }
                else
                {
                    tb.Text = textVal.AsString();
                }

               
            }

            isload = false;
            return tb;
        }
    }


}
