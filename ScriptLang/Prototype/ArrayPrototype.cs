using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptLang.Prototype;

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

            "add" => CreateFunction("add", async args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("add() 期望一个参数");
                arr.Elements.Add(args[0]);
                return Value.Null;
            }),


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
