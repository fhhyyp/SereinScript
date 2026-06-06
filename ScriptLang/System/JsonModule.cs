using System.Text.Json;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    /// <summary>
    /// JSON 处理模块
    /// </summary>    
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class JsonModule : ScriptRuntimeObject<JsonModule>
    {
        private static readonly JsonSerializerOptions _indentedOptions = new() { WriteIndented = true };
        private static readonly JsonSerializerOptions _minifiedOptions = new() { WriteIndented = false };

        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is JsonModule;

        /// <summary>
        /// 将脚本值序列化为 JSON 字符串
        /// </summary>
        /// <param name="value">要序列化的值</param>
        /// <param name="indent">是否缩进（默认 false）</param>
        /// <returns>JSON 字符串</returns>
        [PrototypeFunction]
        public static StringValue Stringify(Value value, BoolValue indent)
        {
            var obj = ConvertToClrObject(value);
            var options = indent?.Value == true ? _indentedOptions : _minifiedOptions;
            var json = JsonSerializer.Serialize(obj, options);
            return StringValue.Create(json);
        }

        /// <summary>
        /// 解析 JSON 字符串为脚本值
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <returns>解析后的脚本值</returns>
        [PrototypeFunction]
        public static Value Parse(StringValue json)
        {
            var obj = JsonSerializer.Deserialize<object>(json.Value);
            return ConvertToScriptValue(obj);
        }

        /// <summary>
        /// 将脚本值转换为 CLR 对象
        /// </summary>
        private static object? ConvertToClrObject(Value value)
        {
            return value switch
            {
                NullValue => null,
                StringValue s => s.Value,
                NumberValue<int> n => n.Value,
                NumberValue<long> n => n.Value,
                NumberValue<double> n => n.Value,
                NumberValue<decimal> n => (double)n.Value,
                BoolValue b => b.Value,
                ArrayValue a => a.Elements.Select(ConvertToClrObject).ToArray(),
                ObjectValue o => o.Properties.ToDictionary(kv => kv.Key, kv => ConvertToClrObject(kv.Value)),
                _ => value.ToString()
            };
        }

        /// <summary>
        /// 将 CLR 对象转换为脚本值
        /// </summary>
        private static Value ConvertToScriptValue(object? obj)
        {
            return obj switch
            {
                null => Value.Null,
                string s => StringValue.Create(s),
                int i => NumberValueFactory.Create(i),
                long l => NumberValueFactory.Create(l),
                double d => NumberValueFactory.Create(d),
                bool b => BoolValue.Create(b),
                JsonElement element => ConvertJsonElement(element),
                object[] arr => new ArrayValue([.. arr.Select(ConvertToScriptValue)]),
                Dictionary<string, object> dict => new ObjectValue(dict.ToDictionary(kv => kv.Key, kv => ConvertToScriptValue(kv.Value))),
                _ => StringValue.Create(obj.ToString() ?? "")
            };
        }

        /// <summary>
        /// 转换 JSON 元素
        /// </summary>
        private static Value ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null => Value.Null,
                JsonValueKind.String => StringValue.Create(element.GetString() ?? ""),
                JsonValueKind.Number => NumberValueFactory.Create(element.GetDouble()),
                JsonValueKind.True => BoolValue.True,
                JsonValueKind.False => BoolValue.False,
                JsonValueKind.Array => new ArrayValue([.. element.EnumerateArray().Select(ConvertJsonElement)]),
                JsonValueKind.Object => new ObjectValue(element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value))),
                _ => StringValue.Create(element.ToString())
            };
        }
    }
}