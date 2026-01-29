using Avalonia.Controls;
using ScriptAvaloniaApp.Utils.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils
{
    public static class ScriptControlFactory
    {
        private static readonly Dictionary<string, Func<ObjectValue, ScriptEngine, ScriptControlBase>> _map
            = new();

        static ScriptControlFactory()
        {
            ScriptControlFactory.Register(nameof(StackPanel), (n, i) => new ScriptStackPanel(n, i));
            ScriptControlFactory.Register(nameof(TextBlock), (n, i) => new ScriptTextBlock(n, i));
            ScriptControlFactory.Register(nameof(TextBox), (n, i) => new ScriptTextBox(n, i));
            ScriptControlFactory.Register(nameof(Button), (n, i) => new ScriptButton(n, i));
            ScriptControlFactory.Register(nameof(ItemsControl), (n, i) => new ScriptItemsControl(n, i));
            ScriptControlFactory.Register(nameof(Grid), (n, i) => new ScriptGrid(n, i));
            ScriptControlFactory.Register(nameof(CheckBox), (n, i) => new ScriptCheckBox(n, i));
            ScriptControlFactory.Register(nameof(ComboBox), (n, i) => new ScriptComboBox(n, i));
            ScriptControlFactory.Register(nameof(Slider), (n, i) => new ScriptSlider(n, i));
            ScriptControlFactory.Register(nameof(ProgressBar), (n, i) => new ScriptProgressBar(n, i)); 
            ScriptControlFactory.Register(nameof(TabControl), (n, i) => new ScriptTabControl(n, i));


        }


        public static void Register(string name,
            Func<ObjectValue, ScriptEngine, ScriptControlBase> factory)
        {
            _map[name] = factory;
        }

        public static async Task<Control> CreateAsync(string key,
            ObjectValue node, ScriptEngine interpreter)
        {
            var type = key.Split('_')[0];

            if (!_map.TryGetValue(type, out var f))
                throw new Exception($"Unknown control: {type}");
            var control = f(node, interpreter);
            var c  = await control.CreateAsync();
            return c;
        }

        public static bool IsControlType(string key)
        {
            var type = key.Split('_')[0];
            return _map.ContainsKey(type);
        }
    }



}
