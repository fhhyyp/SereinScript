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

        public override Control Create()
        {
            var slider = new Slider();
            ApplyAllProperties(slider);

            if (Node.Properties.TryGetValue("Minimum", out var min)) slider.Minimum = min.AsNumber();
            if (Node.Properties.TryGetValue("Maximum", out var max)) slider.Maximum = max.AsNumber();
            if (Node.Properties.TryGetValue("Value", out var val) && val is FunctionValue func)
            {
                async Task Update()
                {
                    var v = await func.CallAsync(new List<Value>(), Engine);
                    slider.Value = v.AsNumber();
                }
                BindingManager.Register(Update);
                _ = Update();
            }

            // 双向绑定
            if (Node.Properties.TryGetValue("OnInput", out var setterVal) && setterVal is FunctionValue setter)
            {
                slider.PropertyChanged += async (_, e) =>
                {
                    if (e.Property == Slider.ValueProperty)
                    {
                        await setter.CallAsync(new List<Value> { new NumberValue(slider.Value) }, Engine);
                        await BindingManager.RefreshAll();
                    }
                };
            }

            return slider;
        }
    }

}
