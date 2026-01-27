using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ScriptLang.Runtime
{
    public static class BuiltinFunctions
    {
        public static void RegisterAll(Scope scope)
        {
            // typeof
            scope.Define("typeof", new FunctionValue("typeof", (args) =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("typeof expects exactly 1 argument");

                var value = args[0];

                string typeStr = value switch
                {
                    NumberValue => "number",
                    StringValue => "string",
                    BoolValue => "boolean",
                    ArrayValue => "array",
                    ObjectValue => "object",
                    FunctionValue => "function",
                    NullValue => "null",
                    _ => "unknown"
                };

                return new StringValue(typeStr);
            }));


            // print
            scope.Define("print", new FunctionValue("print", args =>
            {
                foreach (var arg in args)
                {
                    Console.Write(arg.AsString() + " ");
                    Debug.Write(arg.AsString() + " ");
                }
                Console.WriteLine();
                Debug.WriteLine("");
                return Value.Null;
            }));

            // range
            scope.Define("range", new FunctionValue("range", args =>
            {
                if (args.Count == 1)
                {
                    int count = (int)args[0].AsNumber();
                    var elements = new List<Value>();
                    for (int i = 0; i < count; i++)
                        elements.Add(new NumberValue(i));
                    return new ArrayValue(elements);
                }
                else if (args.Count == 2)
                {
                    int start = (int)args[0].AsNumber();
                    int end = (int)args[1].AsNumber();
                    var elements = new List<Value>();
                    for (int i = start; i < end; i++)
                        elements.Add(new NumberValue(i));
                    return new ArrayValue(elements);
                }
                throw new RuntimeException("range() expects 1 or 2 arguments");
            }));

            // len
            scope.Define("len", new FunctionValue("len", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("len() expects 1 argument");

                return args[0] switch
                {
                    StringValue s => new NumberValue(s.Value.Length),
                    ArrayValue a => new NumberValue(a.Elements.Count),
                    _ => throw new RuntimeException("len() expects string or array")
                };
            }));

            // map (check existence)
            scope.Define("map", new FunctionValue("map", args =>
            {
                if (args.Count != 2)
                    throw new RuntimeException("map() expects 2 arguments");

                if (args[0] is not ObjectValue obj)
                    throw new RuntimeException("map() expects object");

                if (args[1] is not StringValue key)
                    throw new RuntimeException("map() expects string(key)");

                bool exists = obj.Properties.ContainsKey(key.Value);
                return new BoolValue(exists);
            }));

            // keys
            scope.Define("keys", new FunctionValue("keys", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("keys() expects 1 argument");

                if (args[0] is not ObjectValue obj)
                    throw new RuntimeException("keys() expects object");

                var keys = obj.Properties.Keys.Select(k => new StringValue(k)).Cast<Value>().ToList();
                return new ArrayValue(keys);
            }));

            // bool
            scope.Define("bool", new FunctionValue("bool", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("bool() expects 1 argument");

                return args[0] switch
                {
                    NumberValue n => new BoolValue(n.Value > 0),
                    StringValue s => new BoolValue(bool.TryParse(s.Value, out var value)),
                    BoolValue b => new BoolValue(b.Value),
                    _ => throw new RuntimeException("bool() cannot convert value")
                };
            }));

            // int
            scope.Define("int", new FunctionValue("int", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("int() expects 1 argument");

                return args[0] switch
                {
                    NumberValue n => new NumberValue((int)n.Value),
                    StringValue s => new NumberValue(int.TryParse(s.Value, out var value) ? value : 0),
                    BoolValue b => new NumberValue(b.Value ? 1 : 0),
                    _ => throw new RuntimeException("int() cannot convert value")
                };
            }));

            // double
            scope.Define("double", new FunctionValue("double", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("double() expects 1 argument");

                return args[0] switch
                {
                    NumberValue n => new NumberValue((double)n.Value),
                    StringValue s => new NumberValue(double.TryParse(s.Value, out var value) ? value : 0),
                    BoolValue b => new NumberValue(b.Value ? 1 : 0),
                    _ => throw new RuntimeException("double() cannot convert value")
                };
            }));

            // str
            scope.Define("str", new FunctionValue("str", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("str() expects 1 argument");

                return args[0] switch
                {
                    NumberValue n => new StringValue(n.Value.ToString()),
                    StringValue s => new StringValue(s.Value),
                    BoolValue b => new StringValue(b.Value ? Boolean.TrueString : Boolean.FalseString),
                    _ => throw new RuntimeException("double() cannot convert value")
                };
            }));
        }
    }

}
