using Avalonia.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System.Text;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public class ScriptStackPanel : ScriptControlBase
    {

        public ScriptStackPanel(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override Control Create()
        {
            var panel = new StackPanel();
            ApplyAllProperties(panel);

            foreach (var (key, value) in Node.Properties)
            {
                if (value is ObjectValue child && ScriptControlFactory.IsControlType(key))
                {
                    var ctrl = ScriptControlFactory.Create(key, child, Engine).Create();
                    panel.Children.Add(ctrl);
                }
            }

            return panel;
        }
    }



}
