using Avalonia.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public class ScriptSlider : ScriptControlBase
    {
        public ScriptSlider(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override async Task<Control> CreateAsync()
        {
            var slider = new Slider();
            await ApplyAllPropertiesAsync(slider);

            /*if (Node.TryGetValue("Minimum", out var min)) slider.Minimum = min.AsNumber();
            if (Node.TryGetValue("Maximum", out var max)) slider.Maximum = max.AsNumber();
            if (Node.TryGetValue("Value", out var val) && val.Value is FunctionValue func)
            {
                async Task Update()
                {
                    var v = await func.CallAsync(Engine);
                    slider.Value = v.AsNumber();
                }
                await BindingManager.Register(Update);
            }*/

            // 双向绑定
            /*if (Node.TryGetValue("OnInput", out var setterVal) && setterVal.Value is FunctionValue setter)
            {
                slider.PropertyChanged += async (_, e) =>
                {
                    if (e.Property == Slider.ValueProperty)
                    {
                        var numberValue = new NumberValue(slider.Value);
                        await setter.CallAsync(Engine, numberValue);
                        //await BindingManager.RefreshAll();
                    }
                };
            }*/

            return slider;
        }
    }

}
