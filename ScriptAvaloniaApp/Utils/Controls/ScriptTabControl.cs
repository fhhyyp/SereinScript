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

        public override async Task<Control> CreateAsync()
        {
            var tabControl = new TabControl();
            await ApplyAllPropertiesAsync(tabControl);

            /*if (Node.TryGetValue("Tabs", out var tabsVal) && tabsVal.Value is ArrayValue tabs)
            {
                foreach (var tab in tabs.Elements)
                {
                    if (tab is ObjectValue tabNode)
                    {
                        var tabItem = new TabItem();

                        // Header
                        if (tabNode.TryGetValue("Header", out var headerVal))
                        {
                            if (headerVal.Value is FunctionValue func)
                            {
                                async Task UpdateHeader()
                                {
                                    var v = await func.CallAsync(Engine);
                                    tabItem.Header = v.AsString();
                                }
                                await BindingManager.Register(UpdateHeader);
                            }
                            else
                            {
                                tabItem.Header = headerVal.AsString();
                            }
                        }

                        // Content: 取第一个 ObjectValue 属性作为子控件
                        var contentNode = tabNode.Properties.FirstOrDefault(p => p.Value is ObjectValue);
                        if (contentNode.Value.Value is ObjectValue childNode)
                        {
                            var contentControl = ScriptControlFactory.CreateAsync(contentNode.Key, childNode, Engine);
                            tabItem.Content = contentControl;
                        }

                        tabControl.ItemsSource = tabControl.Items.Cast<object>().Concat(new[] { tabItem }).ToList();
                    }
                }
            }
*/
            return tabControl;
        }
    }


}
