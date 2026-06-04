using GeneratorToolkits.ScriptPrototypeToolkits;
using ScriptLang.Runtime;

namespace ScriptLang.Prototype
{
    /// <summary>
    /// 对象原型拓展方法
    /// </summary>
    [PrototypeExtension]
    public partial class ObjectPrototype
    {
        public partial bool IsTarget(Value value)
        {
            return value.IsObject;
        }

        [PrototypeProperty(Name = "count")]
        private NumberValue<int> Count(ObjectValue obj)
        {
            return NumberValueFactory.Create(obj.Properties.Count);
        }

        [PrototypeFunction(Name = "keys")]
        private ArrayValue Keys(ObjectValue obj)
        {
            return new ArrayValue(
                obj.Properties.Keys
                    .Select(k => (Value)new StringValue(k))
                    .ToList()
            );
        }

        [PrototypeFunction(Name = "values")]
        private ArrayValue Values(ObjectValue obj)
        {
            return new ArrayValue(obj.Properties.Values.ToList());
        }

        [PrototypeFunction(Name = "has")]
        private BoolValue Has(ObjectValue obj, StringValue key)
        {
            return BoolValue.Create(obj.Properties.ContainsKey(key.Value));
        }

        [PrototypeFunction(Name = "get")]
        private Value Get(ObjectValue obj, StringValue key)
        {
            return obj.Properties.TryGetValue(key.Value, out var value)
                ? value
                : Value.Null;
        }

        [PrototypeFunction(Name = "set")]
        private void Set(ObjectValue obj, StringValue key, Value value)
        {
            obj.Set(key.Value, value);
        }

        [PrototypeFunction(Name = "containsKey")]
        private BoolValue ContainsKey(ObjectValue obj, StringValue key)
        {
            return BoolValue.Create(obj.Properties.ContainsKey(key.Value));
        }

        [PrototypeFunction(Name = "remove")]
        private BoolValue Remove(ObjectValue obj, StringValue key)
        {
            return BoolValue.Create(obj.Properties.Remove(key.Value));
        }

        [PrototypeFunction(Name = "clear")]
        private void Clear(ObjectValue obj)
        {
            obj.Properties.Clear();
        }
    }


}