using Avalonia.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public class ScriptTextBlock : ScriptControlBase
    {
        public ScriptTextBlock(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override Control Create()
        {
            var tb = new TextBlock();
            ApplyAllProperties(tb);

            if (Node.Properties.TryGetValue("Text", out var textVal))
            {
                if (textVal is StringValue s)
                    tb.Text = s.Value;
                else if (textVal is FunctionValue func)
                {
                    async Task Update()
                    {
                        var v = await func.CallAsync(new List<Value>(), Engine);
                        tb.Text = v.AsString();
                    }
                    BindingManager.Register(Update);
                    _ = Update();
                }
            }

            return tb;
        }
    }




}
