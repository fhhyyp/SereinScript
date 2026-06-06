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
        private static NumberValue<int> Length(StringValue str)
        {
            return NumberValueFactory.Create(str.Value.Length);
        }

        [PrototypeFunction(Name = "toUpper")]
        private static StringValue ToUpper(StringValue str)
        {
            return StringValue.Create(str.Value.ToUpper());
        }

        [PrototypeFunction(Name = "toLower")]
        private static StringValue ToLower(StringValue str)
        {
            return StringValue.Create(str.Value.ToLower());
        }

        [PrototypeFunction(Name = "trim")]
        private static StringValue Trim(StringValue str)
        {
            return StringValue.Create(str.Value.Trim());
        }

        [PrototypeFunction(Name = "split")]
        private static ArrayValue Split(StringValue str, StringValue separator)
        {
            var parts = str.Value.Split(separator.Value);
            return new ArrayValue([.. parts.Select(p => (Value)StringValue.Create(p))]);
        }

        [PrototypeFunction(Name = "substring")]
        private static StringValue Substring(StringValue str, NumberValue<int> start, NumberValue<int>? length = null)
        {
            int len = length != null ? length.Value : str.Value.Length - start.Value;
            return StringValue.Create(str.Value.Substring(start.Value, len));
        }

        [PrototypeFunction(Name = "contains")]
        private static BoolValue Contains(StringValue str, StringValue value)
        {
            return BoolValue.Create(str.Value.Contains(value.Value));
        }
    }

}
