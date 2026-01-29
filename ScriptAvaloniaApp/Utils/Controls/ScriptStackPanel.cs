using Avalonia.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public class ScriptStackPanel : ScriptControlBase
    {

        public ScriptStackPanel(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override async Task<Control> CreateAsync()
        {
            var panel = new StackPanel();
            //await ApplyAllPropertiesAsync(panel);

            foreach (var (key, value) in Node.Properties)
            {
                if (value is ObjectValue child && ScriptControlFactory.IsControlType(key))
                {
                    var ctrl = await ScriptControlFactory.CreateAsync(key, child, Engine);
                    panel.Children.Add(ctrl);
                }
            }

            return panel;
        }
    }



}
