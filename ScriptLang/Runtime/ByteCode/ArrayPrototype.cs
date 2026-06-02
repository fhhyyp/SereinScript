using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptLang;

/// <summary>
/// 数组原型方法（VM 和解释器共享）
/// </summary>
public static class ArrayPrototype
{
    public static Value? GetMethod(ArrayValue arr, string methodName, ScriptEngine engine)
    {
        return methodName switch
        {
            "count" or "length" => NumberValue<int>.Create(arr.Elements.Count),
            "first" => arr.Elements.Count > 0 ? arr.Elements[0] : Value.Null,
            "last" => arr.Elements.Count > 0 ? arr.Elements[^1] : Value.Null,
            "copy" => new ArrayValue(new List<Value>(arr.Elements)),

            "select" => CreateFunction("select", async args =>
            {
                if (args.Count != 1 || args[0] is not FunctionValue func)
                    throw new RuntimeException("select() 期望一个函数");
                var result = new List<Value>();
                foreach (var item in arr.Elements)
                    result.Add(await func.CallAsync(engine, item));
                return new ArrayValue(result);
            }),

            "where" => CreateFunction("where", async args =>
            {
                if (args.Count != 1 || args[0] is not FunctionValue func)
                    throw new RuntimeException("where() 期望一个函数");
                var result = new List<Value>();
                foreach (var item in arr.Elements)
                {
                    var r = await func.CallAsync(engine, item);
                    if (IsTrue(r)) result.Add(item);
                }
                return new ArrayValue(result);
            }),

            "orderBy" => CreateFunction("orderBy", args => OrderBy(arr, true)),
            "orderByDescending" => CreateFunction("orderByDescending", args => OrderBy(arr, false)),

            "toArray" or "toList" => CreateFunction("toArray", args => new ArrayValue(arr.Elements)),

            _ => null
        };
    }

    // ==================== 辅助 ====================

    private static FunctionValue CreateFunction(string name, Func<List<Value>, Value> func)
        => new(name, func);

    private static FunctionValue CreateFunction(string name, Func<List<Value>, Task<Value>> func)
        => new(name, func);

    private static bool IsTrue(Value v) => v is not NullValue and not BoolValue { Value: false };

    // ==================== 排序 ====================

    private static Value OrderBy(ArrayValue arr, bool ascending)
    {
        var sorted = new List<Value>(arr.Elements);
        sorted.Sort((a, b) =>
        {
            int cmp = CompareValues(a, b);
            return ascending ? cmp : -cmp;
        });
        return new ArrayValue(sorted);
    }

    private static int CompareValues(Value a, Value b)
    {
        if (a.IsNumber && b.IsNumber)
        {
            double da = a.As<double>(), db = b.As<double>();
            return da.CompareTo(db);
        }
        return string.Compare(a.AsString(), b.AsString());
    }
}

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