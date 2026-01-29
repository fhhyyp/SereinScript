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

    public override async Task<Control> CreateAsync()
    {
        var comboBox = new ComboBox();
        bool _isUpdating = false; // 防止递归

        IgnoreProperties.Add("Items");
        IgnoreProperties.Add("OnInput");
        IgnoreProperties.Add("SelectedItem");

        await ApplyAllPropertiesAsync(comboBox);
        // Items
        /*if (Node.TryGetValue("Items", out var itemsVal))
        {
            if (itemsVal.Value is ArrayValue arr)
                comboBox.ItemsSource = arr.Elements.Select(v => v.AsString()).ToList();
            else if (itemsVal.Value is FunctionValue func)
            {
                async Task UpdateItems()
                {
                    if (_isUpdating) return;
                    _isUpdating = true;
                    var v = await func.CallAsync(Engine);
                    if (v is ArrayValue newArr)
                    {
                        var t = newArr.Elements.Select(x => x.AsString()).ToList();
                        comboBox.ItemsSource = t;
                    }
                    _isUpdating = false;
                }


                await BindingManager.Register(UpdateItems);
            }
        }*/

        // SelectedItem
        /*if (Node.TryGetValue("SelectedItem", out var selectedVal) && selectedVal.Value is FunctionValue getter)
        {
            async Task Update()
            {
                if (_isUpdating) return;
                var v = await getter.CallAsync( Engine);
                if (!Equals(comboBox.SelectedItem?.ToString(), v.AsString()))
                {
                    _isUpdating = true;
                    comboBox.SelectedItem = v.AsString();
                    _isUpdating = false;
                }
            }
           await  BindingManager.Register(Update);
        }*/

        // OnInput / SelectionChanged
        /*if (Node.TryGetValue("OnInput", out var setterVal) && setterVal.Value is FunctionValue setter)
        {
            comboBox.SelectionChanged += async (_, __) =>
            {
                if (_isUpdating) return;

                if (comboBox.SelectedItem != null)
                {
                    _isUpdating = true;
                    var item = new StringValue(comboBox.SelectedItem?.ToString() ?? string.Empty);
                    await setter.CallAsync(Engine, item);
                    _isUpdating = false;

                    //await BindingManager.RefreshAll();
                }
            };
        }*/

        await ApplyAllPropertiesAsync(comboBox);

        return comboBox;
    }
}
