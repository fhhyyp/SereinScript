using Avalonia.Controls;
using Avalonia.Controls.Documents;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils
{
    public static class ScriptUIBuilderV3
    {
        public static Control BuildUI(Value uiConfig, ScriptEngine interpreter)
        {
            if (uiConfig is not ObjectValue root)
                throw new Exception("UI root must be ObjectValue");

            var first = root.Properties.First();
            return ScriptControlFactory.Create(first.Key, (ObjectValue)first.Value, interpreter).Create();
        }
        public static object? ValueToClr(Value v, Type t)
        {
            if (v.IsNull) return null;

            if (t == typeof(string))
                return v.AsString();

            if (t == typeof(double) || t == typeof(int))
                return v.AsNumber();

            if (v is ClrObjectValue clr)
                return clr.Target;

            return v.ToString();
        }
    }




}
