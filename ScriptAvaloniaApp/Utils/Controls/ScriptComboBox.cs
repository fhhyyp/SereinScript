using Avalonia.Controls;
using ScriptAvaloniaApp.Utils;
using ScriptAvaloniaApp.Utils.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

public class ScriptComboBox : ScriptControlBase
{
    public ScriptComboBox(ObjectValue node, ScriptEngine interpreter)
        : base(node, interpreter) { }

    public override Control Create()
    {
        var comboBox = new ComboBox();
        bool _isUpdating = false; // 防止递归

        IgnoreProperties.Add("Items");
        IgnoreProperties.Add("OnInput");
        IgnoreProperties.Add("SelectedItem");

        ApplyAllProperties(comboBox);
        // Items
        if (Node.Properties.TryGetValue("Items", out var itemsVal))
        {
            if (itemsVal is ArrayValue arr)
                comboBox.ItemsSource = arr.Elements.Select(v => v.AsString()).ToList();
            else if (itemsVal is FunctionValue func)
            {
                async Task UpdateItems()
                {
                    if (_isUpdating) return;
                    _isUpdating = true;
                    var v = await func.CallAsync(new List<Value>(), Engine);
                    if (v is ArrayValue newArr)
                    {
                        var t = newArr.Elements.Select(x => x.AsString()).ToList();
                        comboBox.ItemsSource = t;
                    }
                    _isUpdating = false;
                }
                BindingManager.Register(UpdateItems);
                _ = UpdateItems();
            }
        }

        // SelectedItem
        if (Node.Properties.TryGetValue("SelectedItem", out var selectedVal) && selectedVal is FunctionValue getter)
        {
            async Task Update()
            {
                if (_isUpdating) return;
                var v = await getter.CallAsync(new List<Value>(), Engine);
                if (!Equals(comboBox.SelectedItem?.ToString(), v.AsString()))
                {
                    _isUpdating = true;
                    comboBox.SelectedItem = v.AsString();
                    _isUpdating = false;
                }
            }

            BindingManager.Register(Update);
            _ = Update();
        }

        // OnInput / SelectionChanged
        if (Node.Properties.TryGetValue("OnInput", out var setterVal) && setterVal is FunctionValue setter)
        {
            comboBox.SelectionChanged += async (_, __) =>
            {
                if (_isUpdating) return;

                if (comboBox.SelectedItem != null)
                {
                    _isUpdating = true;
                    await setter.CallAsync(new List<Value> { new StringValue(comboBox.SelectedItem.ToString()!) }, Engine);
                    _isUpdating = false;

                    await BindingManager.RefreshAll();
                }
            };
        }

        ApplyAllProperties(comboBox);

        return comboBox;
    }
}
