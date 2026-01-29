using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Styling;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ScriptAvaloniaApp.Utils.Controls
{
  

    public class ScriptTextBox : ScriptControlBase
    {
        public ScriptTextBox(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override async Task<Control> CreateAsync()
        {
            var control = new TextBox();

            IgnoreProperties.Add("Text");
            await ApplyAllPropertiesAsync(control);
            // getter
            if (Node.TryGetValue("Text", out var value))
            {
                await CheckBindAsync(control, value,
                                     convertTask: convertTask,
                                     objectTask: objectTask,
                                     arrayTask: arrayTask,
                                     defaultTask: defaultTask);
            }
            if (Node.TryGetValue("@Input", out var input) && input is FunctionValue inputFunction)
            {
                control.TextChanged += async (sender, e) => 
                {
                    await inputFunction.CallAsync(Engine);
                };
            }

            return control;
        }

        #region 数据更新

        private async Task convertTask(TextBox control, Value source, FunctionValue convert, FunctionValue? convertBack)
        {
            async Task UpdateText()
            {
                var result = await convert.CallAsync(Engine, source);
                var content = result.AsString();
                if (control.Text != content) control.UIInvoke(c => c.Text = content);
            }
            await SubConvertAsync(source, UpdateText);
            if(convertBack is not null)
            {
                control.TextChanged += async (sender, e) =>
                {
                    var str = new StringValue(control.Text ?? string.Empty);
                    var result = await convertBack.CallAsync(Engine, str);
                };
            }
        }


        private async Task objectTask(TextBox control, ObjectValue source, StringValue target)
        {
            async Task UpdateText()
            {
                await Task.CompletedTask;
                if (source.TryGetValue(target.Value, out var result))
                {
                    var content = result.AsString();
                    if (control.Text != content) control.UIInvoke(c => c.Text = content);
                }
            }
            await SubObjectChangedAsync(source, target, UpdateText);
            control.TextChanged += (sender, e) =>
            {
                source.Set(target.Value, new StringValue(control.Text ?? string.Empty));
            };
        }

        private async Task arrayTask(TextBox control, ArrayValue source, NumberValue target)
        {
            async Task UpdateText()
            {
                if (target.Value >= source.Length) return;
                await Task.CompletedTask;
                var item = source.Get(target);
                var content = item.AsString();
                if (control.Text != content) control.UIInvoke(c => c.Text = content);
            }
            await SubArrayChangedAsync(source, target, UpdateText);
            control.TextChanged += (sender, e) =>
            {
                if (target.Value >= source.Length) return;
                var value = new StringValue(control.Text ?? string.Empty);
                source.Set((int)target.Value, value, Engine);
            };
        }

        private async Task defaultTask(TextBox control, Value? value)
        {
            control.Text = value?.AsString();
        }

        #endregion


    }


    

}
