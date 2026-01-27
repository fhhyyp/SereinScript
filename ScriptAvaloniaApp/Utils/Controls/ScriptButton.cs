using Avalonia.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System.Collections.Generic;
using System.Diagnostics;

namespace ScriptAvaloniaApp.Utils.Controls
{

    public class ScriptButton : ScriptControlBase
    {

        public ScriptButton(ObjectValue node, ScriptEngine engine)
            : base(node, engine) { }

        public override Control Create()
        {
            var btn = new Button();
            ApplyAllProperties(btn);
            if (Node.Properties.TryGetValue("OnClick", out var v) && v is FunctionValue func)
            {
                btn.Click += async (_, __) =>
                {
                    try
                    {

                        await func.CallAsync([], Engine);
                        await BindingManager.RefreshAll();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                };
            }

            return btn;
        }
    }


}
