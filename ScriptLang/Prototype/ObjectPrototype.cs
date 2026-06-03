using ScriptLang.Runtime;

namespace ScriptLang.Prototype;

public static class ObjectPrototype
{
    public static Value? GetMethod(ObjectValue obj, string methodName)
    {
        return methodName switch
        {
            "keys" => new FunctionValue("keys", args =>
            {
                if (args.Count != 0)
                    throw new RuntimeException("keys() 期望 0 个参数");
                return new ArrayValue(
                    obj.Properties.Keys
                        .Select(k => (Value)new StringValue(k))
                        .ToList()
                );
            }),

            "values" => new FunctionValue("values", args =>
            {
                if (args.Count != 0)
                    throw new RuntimeException("values() 期望 0 个参数");
                return new ArrayValue(
                    obj.Properties.Values.ToList()
                );
            }),

            "has" => new FunctionValue("has", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("has() 期望 1 个参数");
                if (args[0] is not StringValue key)
                    throw new RuntimeException("has() 期望字符串参数");
                return BoolValue.Create(obj.Properties.ContainsKey(key.Value));
            }),

            "get" => new FunctionValue("get", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("get() 期望 1 个参数");
                if (args[0] is not StringValue key)
                    throw new RuntimeException("get() 期望字符串参数");
                return obj.Properties.TryGetValue(key.Value, out var value)
                    ? value
                    : Value.Null;
            }),

            "set" => new FunctionValue("set", args =>
            {
                if (args.Count != 2)
                    throw new RuntimeException("set() 期望 2 个参数");
                if (args[0] is not StringValue key)
                    throw new RuntimeException("set() 第一个参数期望字符串");
                obj.Set(key.Value, args[1]);
                return Value.Null;
            }),

            "count" or "length" => new FunctionValue("count", args =>
            {
                if (args.Count != 0)
                    throw new RuntimeException("count() 期望 0 个参数");
                return NumberValue<int>.Create(obj.Properties.Count);
            }),

            "containsKey" => new FunctionValue("containsKey", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("containsKey() 期望 1 个参数");
                if (args[0] is not StringValue key)
                    throw new RuntimeException("containsKey() 期望字符串参数");
                return BoolValue.Create(obj.Properties.ContainsKey(key.Value));
            }),

            "remove" => new FunctionValue("remove", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("remove() 期望 1 个参数");
                if (args[0] is not StringValue key)
                    throw new RuntimeException("remove() 期望字符串参数");
                return BoolValue.Create(obj.Properties.Remove(key.Value));
            }),

            "clear" => new FunctionValue("clear", args =>
            {
                if (args.Count != 0)
                    throw new RuntimeException("clear() 期望 0 个参数");
                obj.Properties.Clear();
                return Value.Null;
            }),

            _ => null
        };
    }
}