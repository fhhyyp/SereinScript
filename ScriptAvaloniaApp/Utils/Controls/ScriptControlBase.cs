using Avalonia.Controls;
using Avalonia.Media;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public abstract class ScriptControlBase
    {
        protected readonly HashSet<string> IgnoreProperties = new HashSet<string>();

        protected ObjectValue Node { get; }
        protected ScriptEngine Engine { get; }

        protected ScriptControlBase(ObjectValue node, ScriptEngine engine)
        {
            Node = node;
            Engine = engine;
        }

        public abstract Control Create();

        protected void ApplyAllProperties(Control control)
        {
            if(IgnoreProperties.Count > 0)
            {
                foreach (var (key, value) in Node.Properties)
                {
                    // 跳过子控件
                    if (IgnoreProperties.Contains(key) && value is ObjectValue && ScriptControlFactory.IsControlType(key))
                        continue;

                    TryBind(control, key, value);
                }
            }
            else
            {
                foreach (var (key, value) in Node.Properties)
                {
                    // 跳过子控件
                    if (value is ObjectValue && ScriptControlFactory.IsControlType(key))
                        continue;

                    TryBind(control, key, value);
                }
            }
        }

        protected void TryBind(Control control, string propertyName, Value value)
        {
            var prop = control.GetType().GetProperty(propertyName);
            if (prop == null || !prop.CanWrite)
                return;

            // 常量
            if (value is FunctionValue)
            {
                // 绑定函数
                var func = (FunctionValue)value;

                async Task Update()
                {
                    var v = await func.CallAsync(new List<Value>(), Engine);
                    var clr = ConvertValueToClr(v, prop.PropertyType);
                    prop.SetValue(control, clr);
                }

                BindingManager.Register(Update);
                _ = Update();
            }
            else
            {
                var clr = ConvertValueToClr(value, prop.PropertyType);
                prop.SetValue(control, clr);
                return;
            }
        }

        // ---------------- 核心：Value → CLR ----------------

        protected object? ConvertValueToClr(Value v, Type targetType)
        {
            if (v.IsNull)
                return null;

            // string
            if (targetType == typeof(string))
                return v.AsString();

            // number
            if (targetType == typeof(int))
                return (int)v.AsNumber();
            if (targetType == typeof(double))
                return v.AsNumber();
            if (targetType == typeof(float))
                return (float)v.AsNumber();

            // bool
            //if (targetType == typeof(bool))
            //    return v.AsBool();
            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                if (v.IsBool) return v.AsBool();
                if (v.IsString && bool.TryParse(v.AsString(), out var b)) return b;
                throw new InvalidCastException($"Cannot convert {v} to bool");
            }

            // enum
            if (targetType.IsEnum)
            {
                var s = v.AsString();
                return Enum.Parse(targetType, s, ignoreCase: true);
            }

            // Color
            if (targetType == typeof(Avalonia.Media.Color))
            {
                var s = v.AsString();
                return ParseColor(s);
            }

            // Thickness
            if (targetType == typeof(Avalonia.Thickness))
            {
                return ParseThickness(v.AsString());
            }

            // CLR object passthrough
            if (v is ClrObjectValue clr)
            {
                if (targetType.IsAssignableFrom(clr.Target.GetType()))
                    return clr.Target;
            }

            // fallback
            return v.AsString();
        }

        private Avalonia.Thickness ParseThickness(string s)
        {
            var parts = s.Split(',').Select(x => double.Parse(x.Trim())).ToArray();
            return parts.Length switch
            {
                1 => new Avalonia.Thickness(parts[0]),
                2 => new Avalonia.Thickness(parts[0], parts[1]),
                4 => new Avalonia.Thickness(parts[0], parts[1], parts[2], parts[3]),
                _ => throw new Exception($"Invalid Thickness: {s}")
            };
        }
        public static Color ParseColor(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Color string is null or empty");

            input = input.Trim();

            // 1. 直接走 Avalonia 内置解析
            if (Color.TryParse(input, out var color))
                return color;

            // 2. 处理 "128,128,128" / "128,128,128,255"
            if (input.Contains(','))
            {
                var parts = input.Split(',');
                if (parts.Length == 3 || parts.Length == 4)
                {
                    byte r = byte.Parse(parts[0], CultureInfo.InvariantCulture);
                    byte g = byte.Parse(parts[1], CultureInfo.InvariantCulture);
                    byte b = byte.Parse(parts[2], CultureInfo.InvariantCulture);

                    if (parts.Length == 3)
                        return Color.FromRgb(r, g, b);

                    byte a = byte.Parse(parts[3], CultureInfo.InvariantCulture);
                    return Color.FromArgb(a, r, g, b);
                }
            }

            throw new FormatException($"Unsupported color format: {input}");
        }

    }



}


/*
 
        private Avalonia.Thickness ParseThickness(string s)
        {
            var parts = s.Split(',').Select(x => double.Parse(x.Trim())).ToArray();
            return parts.Length switch
            {
                1 => new Avalonia.Thickness(parts[0]),
                2 => new Avalonia.Thickness(parts[0], parts[1]),
                4 => new Avalonia.Thickness(parts[0], parts[1], parts[2], parts[3]),
                _ => throw new Exception($"Invalid Thickness: {s}")
            };
        }
        public static Color ParseColor(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Color string is null or empty");

            input = input.Trim();

            // 1. 直接走 Avalonia 内置解析
            if (Color.TryParse(input, out var color))
                return color;

            // 2. 处理 "128,128,128" / "128,128,128,255"
            if (input.Contains(','))
            {
                var parts = input.Split(',');
                if (parts.Length == 3 || parts.Length == 4)
                {
                    byte r = byte.Parse(parts[0], CultureInfo.InvariantCulture);
                    byte g = byte.Parse(parts[1], CultureInfo.InvariantCulture);
                    byte b = byte.Parse(parts[2], CultureInfo.InvariantCulture);

                    if (parts.Length == 3)
                        return Color.FromRgb(r, g, b);

                    byte a = byte.Parse(parts[3], CultureInfo.InvariantCulture);
                    return Color.FromArgb(a, r, g, b);
                }
            }

            throw new FormatException($"Unsupported color format: {input}");
        }*/
