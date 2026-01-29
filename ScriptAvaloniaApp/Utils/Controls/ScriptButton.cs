using Avalonia.Controls;
using Avalonia.Interactivity;
using ScriptLang;
using ScriptLang.Runtime;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{

    public class ScriptButton : ScriptControlBase
    {

        public ScriptButton(ObjectValue node, ScriptEngine engine)
            : base(node, engine) { }

        public override async Task<Control> CreateAsync()
        {
            var btn = new Button();
            await ApplyAllPropertiesAsync(btn);
            if (Node.TryGetValue("@Click", out var v) && v is FunctionValue func)
            {
                async void @Clieck(object? sender, RoutedEventArgs e)
                {
                    try
                    {
                        await func.CallAsync(Engine);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }
                btn.Click += @Clieck;
            }

            return btn;
        }
    }


}
