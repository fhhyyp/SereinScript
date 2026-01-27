using Avalonia.Controls;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils
{
    public static class ScriptUIBuilder
    {
        public static Control BuildUI(Value uiConfig, Interpreter interpreter)
        {
            if (uiConfig is not ObjectValue obj)
                throw new ArgumentException("UI config must be ObjectValue");

            string type = obj.Get<StringValue>("type").Value
                          ?? throw new Exception("type required");

            Control control = type switch
            {
                "StackPanel" => new StackPanel(),
                "TextBlock" => new TextBlock(),
                "Button" => new Button(),
                _ => throw new Exception($"Unknown control type: {type}")
            };

            // ================= text binding =================

            if (obj.ContainsKey("text"))
            {
                var textVal = obj.Get<Value>("text");

                if (control is TextBlock tb)
                {
                    BindText(tb, textVal, interpreter);
                }

                if (control is Button btn)
                {
                    BindButtonText(btn, textVal, interpreter);
                }

                if (control is TextBox textBox && textVal is ObjectValue objectValue)
                {
                    BindTextBox(textBox, objectValue, interpreter);
                }
            }

            // ================= children =================

            if (obj.ContainsKey("children") && control is Panel panel)
            {
                var children = obj.Get<ArrayValue>("children");
                foreach (var child in children.Elements.OfType<ObjectValue>())
                {
                    if(child is ObjectValue objectValue)
                    {
                        var c = BuildUI(child, interpreter);
                        panel.Children.Add(c);
                    }
                }
            }

            // ================= onClick =================

            if (obj.ContainsKey("onClick") && control is Button button)
            {
                var func = obj.Get<FunctionValue>("onClick");

                button.Click += async (_, __) =>
                {
                    await func.CallAsync(new List<Value>(), interpreter);

                    // 关键：状态改变后刷新所有绑定
                    await BindingManager.RefreshAll();
                };
            }

            return control;
        }

        // ================= helpers =================

        private static void BindText(
            TextBlock textBlock,
            Value value,
            Interpreter interpreter)
        {
            if (value is StringValue str)
            {
                textBlock.Text = str.Value ?? "";
                return;
            }

            if (value is FunctionValue func)
            {
                async Task Update()
                {
                    var v = await func.CallAsync(new List<Value>(), interpreter);
                    textBlock.Text = ValueToString(v);
                }

                BindingManager.Register(Update);
                _ = Update(); // 初次计算
            }
        }

        private static void BindButtonText(
            Button button,
            Value value,
            Interpreter interpreter)
        {
            if (value is StringValue str)
            {
                button.Content = str.Value ?? "";
                return;
            }

            if (value is FunctionValue func)
            {
                async Task Update()
                {
                    var v = await func.CallAsync(new List<Value>(), interpreter);
                    button.Content = ValueToString(v);
                }

                BindingManager.Register(Update);
                _ = Update();
            }
        }


        private static void BindTextBox(
                Avalonia.Controls.TextBox textBox,
                ObjectValue node,
                Interpreter interpreter)
        {
            if (!node.ContainsKey("Value") || !node.ContainsKey("OnChange"))
                return;

            var getter = node.Get<FunctionValue>("Value");
            var setter = node.Get<FunctionValue>("OnChange");

            // ====== 从状态 → UI ======
            async Task UpdateFromState()
            {
                var v = await getter.CallAsync(new List<Value>(), interpreter);
                textBox.Text = v.AsString();
            }

            BindingManager.Register(UpdateFromState);
            _ = UpdateFromState();

            // ====== 从 UI → 状态 ======
            textBox.TextChanged += async (_, __) =>
            {
                var val = new StringValue(textBox.Text ?? "");
                await setter.CallAsync(new List<Value> { val }, interpreter);

                // 状态变了，刷新所有绑定
                await BindingManager.RefreshAll();
            };
        }


        private static string ValueToString(Value v)
        {
            return v switch
            {
                StringValue s => s.Value ?? "",
                NumberValue n => n.Value.ToString(),
                BoolValue b => b.Value.ToString(),
                NullValue => "",
                _ => v.ToString() ?? ""
            };
        }
    }

}
