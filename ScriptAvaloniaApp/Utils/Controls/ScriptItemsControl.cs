using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public class ScriptItemsControl : ScriptControlBase
    {
        public ScriptItemsControl(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override async Task<Control> CreateAsync()
        {
            var itemsControl = new ItemsControl();

            // 动态绑定模式
            if (Node.TryGetValue("ItemTemplate", out var tmplVal) && tmplVal.Value is FunctionValue templateFunc
                && Node.TryGetValue("Items", out var itemsVal) && itemsVal.Value is ArrayValue arr)
            {

                async Task Refresh()
                {
                    List<Control> controls = new List<Control>();
                    Debug.WriteLine(arr);
                    foreach (var item  in arr.Elements)
                    {
                        var uiValue = await templateFunc.CallAsync(Engine, item);
                        if (uiValue is not ObjectValue obj)
                            throw new Exception("ItemTemplate must return ObjectValue");

                        var first = obj.Properties.First();
                        try
                        {
                            var child = await ScriptControlFactory.CreateAsync(first.Key, (ObjectValue)first.Value.Value, Engine);
                            controls.Add(child);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }
                    itemsControl.ItemsSource = controls;
                }
                await  SubArrayChangedAsync(arr, Refresh);

                //throw new Exception("ItemsControl requires either Items+ItemTemplate (dynamic) or Items array (static)");
            }


            // 静态绑定模式
#if false
            else if (Node.TryGetValue("Items", out itemsVal) &&
                         itemsVal.Value is ArrayValue arrVal)
            {
                var list = new List<Control>();
                foreach (var item in arrVal.Elements)
                {
                    if (item is ObjectValue obj)
                    {
                        var first = obj.Properties.First();
                        var child = await ScriptControlFactory.CreateAsync(first.Key, (ObjectValue)first.Value.Value, Engine);
                        list.Add(child);
                    }
                }
                itemsControl.ItemsSource = list;
            } 
#endif
            else
            {
                throw new Exception("ItemsControl requires either Items+ItemTemplate (dynamic) or Items array (static)");
            }

            return itemsControl;
        }
    }
}
