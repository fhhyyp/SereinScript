using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

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

        public abstract Task<Control> CreateAsync();

        protected async Task ApplyAllPropertiesAsync(Control control)
        {
            foreach (var (key, value) in Node.Properties)
            {
                // 跳过子控件
                if (IgnoreProperties.Contains(key) && value.Value is ObjectValue && ScriptControlFactory.IsControlType(key))
                    continue;

                await TryBind(control, key, value);
            }
        }

        protected async Task TryBind(Control control, string propertyName, Value value)
        {
            var prop = control.GetType().GetProperty(propertyName);
            if (prop == null || !prop.CanWrite)
                return;

            if (TryGetConvert(value, out var source, out var convert))
            {
                // 绑定转换器
                async Task Refresh()
                {
                    var result = await convert.CallAsync(Engine, source);
                    var clr = ConvertValueToClr(result, prop.PropertyType);
                    control.UIInvoke(c => 
                        prop.SetValue(c, clr));
                }
                await SubChangedAsync(source, Refresh);
            }
            if (TryGetObjectValue(value, out var sourceObject, out var targetKey))
            {
                // 如果这个值来源于一个对象，并且该对象对应值（名称）发生改变，则更新控件属性
                async Task Refresh()
                {
                    if (propertyName == "IsChecked")
                    {

                    }
                    var value = sourceObject.Get(targetKey);
                    var clr = ConvertValueToClr(value, prop.PropertyType);
                    control.UIInvoke(c => prop.SetValue(c, clr));
                }
                await SubChangedAsync(sourceObject, Refresh);
            }
            if (TryGetArrayValue(value, out var sourceArray, out var targetIndex))
            {
                // 如果这个值来源于一个数组，并且该对象对应值（下标）发生改变，则更新控件属性
                async Task Refresh()
                {
                    var value = sourceArray.Get(targetIndex);
                    var clr = ConvertValueToClr(value, prop.PropertyType);
                    control.UIInvoke(c => prop.SetValue(c, clr));
                }
                await SubChangedAsync(sourceArray, Refresh);
            }
            else
            {
                // 正常数据绑定，无数据通知功能
                var clr = ConvertValueToClr(value, prop.PropertyType);
                prop.SetValue(control, clr);
            }

        }

        /// <summary>
        /// 判断对象值是否为转换器结构
        /// </summary>
        /// <param name="menberValue"></param>
        /// <param name="source"></param>
        /// <param name="convert"></param>
        /// <returns></returns>
        protected static bool TryGetConvert(Value menberValue, [NotNullWhen(true)] out Value? source, [NotNullWhen(true)] out FunctionValue? convert)
        {
            if (menberValue is MemberValue member && member.Value is ObjectValue objectValue
                && objectValue.TryGetValue("Value", out var v)
                && objectValue.TryGetValue("Convert", out var c) && c.Value is FunctionValue func)
            {
                source = v.Value;
                convert = func;
                return true;
            }
            else
            {
                source = null;
                convert = null;
                return false;
            }
        }

        /// <summary>
        /// 判断值来源是否为对象
        /// </summary>
        /// <param name="menberValue"></param>
        /// <param name="source"></param>
        /// <param name="convert"></param>
        /// <returns></returns>
        protected static bool TryGetObjectValue(Value menberValue, [NotNullWhen(true)] out ObjectValue? sourceObject,  [NotNullWhen(true)] out string? targetKey)
        {
            if (menberValue is MemberValue member && member.Value.Source is ObjectValue objectValue
                && !string.IsNullOrWhiteSpace(member.Value.TargetKey))
            {
                sourceObject = objectValue;
                targetKey = member.Value.TargetKey;
                return true;
            }
            else
            {
                sourceObject = null;
                targetKey = null;
                return false;
            }
        }
        

        /// <summary>
        /// 判断值来源是否为数组
        /// </summary>
        /// <param name="menberValue"></param>
        /// <param name="source"></param>
        /// <param name="convert"></param>
        /// <returns></returns>
        protected static bool TryGetArrayValue(Value menberValue, [NotNullWhen(true)] out ArrayValue? sourceArray,  [NotNullWhen(true)] out int targetIndex)
        {
            if (menberValue is MemberValue member && member.Value.Source is ArrayValue value
                && member.Value.TargetIndex > -1)
            {
                sourceArray = value;
                targetIndex = member.Value.TargetIndex;
                return true;
            }
            else
            {
                sourceArray = null;
                targetIndex = -1;
                return false;
            }
        }

        /// <summary>
        /// 从 DSL Value 转换为 CLR Value
        /// </summary>
        /// <param name="v"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        /// <exception cref="InvalidCastException"></exception>
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
            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                if (v.IsBool) return v.AsBool();
                if (v.IsString && bool.TryParse(v.AsString(), out var b)) return b;
                return false;
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
                return ParseThickness(v);
            }

            // CLR object passthrough
            if (v is ClrObjectValue clr)
            {
                if (targetType.IsAssignableFrom(clr.Target?.GetType()))
                    return clr.Target;
            }

            // fallback
            return v.AsString();
        }

        private Avalonia.Thickness ParseThickness(Value v)
        {
            if(v is MemberValue memberValue)
            {
                v = memberValue.Value;
            }
            double[] thicks = v switch
            {
                ArrayValue arr => arr.Elements.Select(x => x.AsNumber()).ToArray(),
                _ => v.AsString().Split(',').Select(x => double.Parse(x.Trim())).ToArray(),
            };

            return thicks.Length switch
            {
                1 => new Avalonia.Thickness(thicks[0]),
                2 => new Avalonia.Thickness(thicks[0], thicks[1]),
                4 => new Avalonia.Thickness(thicks[0], thicks[1], thicks[2], thicks[3]),
                _ => throw new Exception($"Invalid Thickness: {v}")
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

        internal async Task SubConvertAsync(Value value,  Func<Task> refresh)
        {
            if (value is IObservableValue observable)
            {
                void Observable_Changed(ValueChange e)
                {
                    _ = refresh.Invoke();
                }
                observable.Changed += Observable_Changed;
            }
            await refresh.Invoke();
        }

        internal async Task SubObjectChangedAsync(ObjectValue value, string? targetKey, Func<Task> refresh)
        {
            if (string.IsNullOrWhiteSpace(targetKey)) return;
            if (value is IObservableValue observable)
            {
                void Observable_Changed(ValueChange e)
                {
                    if (e.Key == targetKey)
                        _ = refresh.Invoke();
                }
                observable.Changed += Observable_Changed;
            }
            await refresh.Invoke();
        }

        internal async Task SubArrayChangedAsync(ArrayValue value, Func<Task> refresh)
        {
            if (value is IObservableValue observable)
            {
                void Observable_Changed(ValueChange e)
                {
                    if (e.Key == nameof(Array.Length)) return;
                        _ = refresh.Invoke();
                }
                observable.Changed += Observable_Changed;
            }
            await refresh.Invoke();
        }

        internal async Task SubArrayChangedAsync(ArrayValue value, int targetIndex, Func<Task> refresh)
        {
            if (value is IObservableValue observable)
            {
                void Observable_Changed(ValueChange e)
                {
                    if (e.Key == nameof(Array.Length)) return;
                    if (!int.TryParse(e.Key, out var index)) return;
                    if(targetIndex != index)
                        _ = refresh.Invoke();
                }
                observable.Changed += Observable_Changed;
            }
            await refresh.Invoke();
        }


        internal async Task SubChangedAsync(Value value, Func<Task> refresh)
        {
            
            if(value is IObservableValue observable)
            {
                void Observable_Changed(ValueChange e)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await refresh.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    });
                }
                observable.Changed += Observable_Changed;
            }
            await refresh.Invoke();
        }




        internal void SubChanged(Value value, Action refresh)
        {
            if(value is IObservableValue observable)
            {
                void Observable_Changed(ValueChange obj)
                {
                    refresh.Invoke();
                }
                observable.Changed += Observable_Changed;
            }
            refresh.Invoke();
        }

    }

}

