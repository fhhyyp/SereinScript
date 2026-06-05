using ScriptLang.Runtime;

namespace ScriptLang.Prototype
{

    /// <summary>
    /// 字符串原型拓展方法
    /// </summary>
    [PrototypeExtension]
    public partial class StringPrototype
    {
        public partial bool IsTarget(Value value)
        {
            return value.IsString;
        }
        [PrototypeProperty(Name = "length")]
        private NumberValue<int> Length(StringValue str)
        {
            return NumberValueFactory.Create(str.Value.Length);
        }

        [PrototypeFunction(Name = "toUpper")]
        private StringValue ToUpper(StringValue str)
        {
            return new StringValue(str.Value.ToUpper());
        }

        [PrototypeFunction(Name = "toLower")]
        private StringValue ToLower(StringValue str)
        {
            return new StringValue(str.Value.ToLower());
        }

        [PrototypeFunction(Name = "trim")]
        private StringValue Trim(StringValue str)
        {
            return new StringValue(str.Value.Trim());
        }

        [PrototypeFunction(Name = "split")]
        private ArrayValue Split(StringValue str, StringValue separator)
        {
            var parts = str.Value.Split(separator.Value);
            return new ArrayValue(parts.Select(p => (Value)new StringValue(p)).ToList());
        }

        [PrototypeFunction(Name = "substring")]
        private StringValue Substring(StringValue str, NumberValue<int> start, NumberValue<int>? length = null)
        {
            int len = length != null ? length.Value : str.Value.Length - start.Value;
            return new StringValue(str.Value.Substring(start.Value, len));
        }

        [PrototypeFunction(Name = "contains")]
        private BoolValue Contains(StringValue str, StringValue value)
        {
            return BoolValue.Create(str.Value.Contains(value.Value));
        }
    }

}
