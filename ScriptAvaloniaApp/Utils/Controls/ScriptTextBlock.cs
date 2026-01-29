using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ScriptLang;
using ScriptLang.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public class ScriptTextBlock : ScriptControlBase
    {
        public ScriptTextBlock(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override async Task<Control> CreateAsync()
        {
            var tb = new TextBlock();

            IgnoreProperties.Add("Text");

            await ApplyAllPropertiesAsync(tb);

            if (Node.TryGetValue("Text", out var textVal))
            {
                if (TryGetConvert(textVal, out var source, out var convert))
                {
                    async Task Refresh()
                    {
                        var result = await convert.CallAsync(Engine, source);
                        var v = result.AsString();
                        if (v != tb.Text)
                        {
                            tb.UIInvoke(c => c.Text = v);
                        }
                    }
                    await SubConvertAsync(source, Refresh);
                }
                else if (textVal is ArrayValue arrayValue)
                {
                    async Task Refresh()
                    {
                        await Task.CompletedTask;
                        var v = string.Join(string.Empty, arrayValue.Elements.Select(x => x.AsString()));
                        if (v != tb.Text)
                        {
                            tb.UIInvoke(c => c.Text = v);
                        }
                    }
                    await SubArrayChangedAsync(arrayValue, Refresh);
                }
                else
                {
                    tb.Text = textVal.AsString();
                }
            }
            return tb;
        }
    }




}
