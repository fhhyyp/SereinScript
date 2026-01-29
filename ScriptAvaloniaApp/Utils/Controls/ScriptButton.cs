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
            if (Node.TryGetValue("@Click", out var v) && v.Value is FunctionValue func)
            {
                /*if(func.ParameterCount == 1)
                {
                    async void @Clieck(object? sender, RoutedEventArgs e)
                    {
                        try
                        {
                            var obj = new ObjectValue(new Dictionary<string, MemberValue>());
                            obj.Set("sender", sender is Button btn ? new ClrObjectValue(btn) : Value.Null);
                            obj.Set("e", new ClrObjectValue(e));

                            await func.CallAsync(Engine, obj);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }
                    btn.Click += @Clieck;
                }
                else*/
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
            }

            return btn;
        }
    }


}
