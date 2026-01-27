using Avalonia.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public class ScriptTabControl : ScriptControlBase
    {
        public ScriptTabControl(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override Control Create()
        {
            var tabControl = new TabControl();
            ApplyAllProperties(tabControl);

            if (Node.Properties.TryGetValue("Tabs", out var tabsVal) && tabsVal is ArrayValue tabs)
            {
                foreach (var tab in tabs.Elements)
                {
                    if (tab is ObjectValue tabNode)
                    {
                        var tabItem = new TabItem();

                        // Header
                        if (tabNode.Properties.TryGetValue("Header", out var headerVal))
                        {
                            if (headerVal is FunctionValue func)
                            {
                                async Task UpdateHeader()
                                {
                                    var v = await func.CallAsync(new List<Value>(), Engine);
                                    tabItem.Header = v.AsString();
                                }
                                BindingManager.Register(UpdateHeader);
                                _ = UpdateHeader();
                            }
                            else
                            {
                                tabItem.Header = headerVal.AsString();
                            }
                        }

                        // Content: 取第一个 ObjectValue 属性作为子控件
                        var contentNode = tabNode.Properties.FirstOrDefault(p => p.Value is ObjectValue);
                        if (contentNode.Value is ObjectValue childNode)
                        {
                            var contentControl = ScriptControlFactory.Create(contentNode.Key, childNode, Engine).Create();
                            tabItem.Content = contentControl;
                        }

                        tabControl.ItemsSource = tabControl.Items.Cast<object>().Concat(new[] { tabItem }).ToList();
                    }
                }
            }

            return tabControl;
        }
    }


}
