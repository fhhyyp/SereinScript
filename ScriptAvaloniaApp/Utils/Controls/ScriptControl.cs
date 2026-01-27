using Avalonia.Controls;
using ScriptLang.Runtime;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public abstract class ScriptControl
    {
        public ObjectValue Node { get; }
        public Interpreter Interpreter { get; }

        protected ScriptControl(ObjectValue node, Interpreter interpreter)
        {
            Node = node;
            Interpreter = interpreter;
        }

        /// <summary>
        /// 创建 Avalonia 控件实例
        /// </summary>
        public abstract Control CreateControl();

        /// <summary>
        /// 绑定属性与事件
        /// </summary>
        protected void BindProperty<T>(Control control, string propertyName, Value value)
        {
            var prop = control.GetType().GetProperty(propertyName);
            if (prop == null) return;

            if (value is StringValue s)
                prop.SetValue(control, s.Value);
            else if (value is NumberValue n)
                prop.SetValue(control, n.Value);
            else if (value is FunctionValue func)
            {
                async Task Update()
                {
                    var v = await func.CallAsync(new List<Value>(), Interpreter);
                    prop.SetValue(control, ScriptUIBuilderV3.ValueToClr(v, prop.PropertyType));
                }
                BindingManager.Register(Update);
                _ = Update();
            }
        }
    }

}
