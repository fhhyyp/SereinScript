using Avalonia.Controls;
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

        public override Control Create()
        {
            var itemsControl = new ItemsControl();

            // 动态绑定模式
            if (Node.Properties.TryGetValue("Items", out var itemsVal) &&
                itemsVal is FunctionValue itemsFunc &&
                Node.Properties.TryGetValue("ItemTemplate", out var tmplVal) &&
                tmplVal is FunctionValue templateFunc)
            {
                async Task Refresh()
                {
                    try
                    {
                        var v = await itemsFunc.CallAsync([], Engine);
                        if (v is not ArrayValue arr)
                            throw new Exception("Items must return ArrayValue");

                        var list = new List<Control>();
                        foreach (var item in arr.Elements)
                        {
                            var uiValue = await templateFunc.CallAsync(new List<Value> { item }, Engine);
                            if (uiValue is not ObjectValue obj)
                                throw new Exception("ItemTemplate must return ObjectValue");

                            var first = obj.Properties.First();
                            var child = ScriptControlFactory.Create(first.Key, (ObjectValue)first.Value, Engine).Create();
                            list.Add(child);
                        }

                        itemsControl.ItemsSource = list;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }

                BindingManager.Register(Refresh);
                _ = Refresh();
            }
            // 静态定义模式
            else if (Node.Properties.TryGetValue("Items", out itemsVal) &&
                     itemsVal is ArrayValue arrVal)
            {
                var list = new List<Control>();
                foreach (var item in arrVal.Elements)
                {
                    if (item is ObjectValue obj)
                    {
                        var first = obj.Properties.First();
                        var child = ScriptControlFactory.Create(first.Key, (ObjectValue)first.Value, Engine).Create();
                        list.Add(child);
                    }
                }
                itemsControl.ItemsSource = list;
            }
            else
            {
                throw new Exception("ItemsControl requires either Items+ItemTemplate (dynamic) or Items array (static)");
            }

            return itemsControl;
        }
    }
}
