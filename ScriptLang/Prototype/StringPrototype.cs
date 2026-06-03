using ScriptLang.Runtime;

namespace ScriptLang.Prototype;

/// <summary>
/// 字符串原型方法
/// </summary>
public static class StringPrototype
{
    public static Value? GetMethod(StringValue str, string methodName)
    {
        return methodName switch
        {
            "length" => NumberValue<int>.Create(str.Value.Length),
            "toUpper" => new FunctionValue("toUpper", _ => new StringValue(str.Value.ToUpper())),
            "toLower" => new FunctionValue("toLower", _ => new StringValue(str.Value.ToLower())),
            "trim" => new FunctionValue("trim", _ => new StringValue(str.Value.Trim())),
            "split" => new FunctionValue("split", args =>
            {
                if (args.Count != 1 || args[0] is not StringValue sep)
                    throw new RuntimeException("split() 期望一个字符串分隔符");
                var parts = str.Value.Split(sep.Value);
                return new ArrayValue(parts.Select(p => (Value)new StringValue(p)).ToList());
            }),
            "substring" => new FunctionValue("substring", args =>
            {
                int start = args[0].As<int>();
                int length = args.Count > 1 ? args[1].As<int>() : str.Value.Length - start;
                return new StringValue(str.Value.Substring(start, length));
            }),
            "contains" => new FunctionValue("contains", args =>
            {
                if (args.Count != 1 || args[0] is not StringValue s)
                    throw new RuntimeException("contains() 期望一个字符串");
                return BoolValue.Create(str.Value.Contains(s.Value));
            }),
            _ => null
        };
    }
}
