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
            
            scope.Define("gc", new FunctionValue("gc", async (args) =>
            {
                GC.Collect();
                return Value.Null;
            }));
            
            scope.Define("sleep", new FunctionValue("sleep", async (args) =>
            {
                if (args.Count != 1 || !args[0].IsNumber_Int)
                    throw new RuntimeException("sleep() 期望 1 个参数");

                var index = args[0].As<int>();
                await Task.Delay(index);
                return Value.Null;
            }));

            // typeof
            scope.Define("typeof", new FunctionValue("typeof", (args) =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("typeof() 期望 1 个参数");

                var value = args[0];

                string typeStr = value switch
                {
                    NumberValue<byte> => "byte",
                    NumberValue<short> => "short",
                    NumberValue<int> => "int",
                    NumberValue<long> => "long",
                    NumberValue<float> => "float",
                    NumberValue<double> => "double",
                    NumberValue<decimal> => "decimal",

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
                    int count = args[0].As<int>();
                    var elements = new List<Value>();
                    for (int i = 0; i < count; i++)
                        elements.Add(NumberValue<int>.Create(i));
                    return new ArrayValue(elements);
                }
                else if (args.Count == 2)
                {
                    int start = args[0].As<int>();
                    int end = args[1].As<int>();
                    var elements = new List<Value>();
                    for (int i = start; i < end; i++)
                        elements.Add(NumberValue<int>.Create(i));
                    return new ArrayValue(elements);
                }
                throw new RuntimeException("range() 期望 1 或 2 个参数");
            }));

            // len
            scope.Define("len", new FunctionValue("len", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("len() 期望 1 个参数");
                var lenValue = args[0] switch
                {
                    StringValue s => NumberValue<int>.Create(s.Value.Length),
                    ArrayValue a => NumberValue<int>.Create(a.Length),
                    _ => throw new RuntimeException("len() 期望字符串或数组")
                };
                return lenValue;
            }));

            // map (check existence)
            /*scope.Define("maphasey", new FunctionValue("map", args =>
            {
                if (args.Count != 2)
                    throw new RuntimeException("map() 期望 2 个参数");

                if (args[0] is not ObjectValue obj)
                    throw new RuntimeException("map() 期望对象");

                if (args[1] is not StringValue key)
                    throw new RuntimeException("map() 期望字符串键");

                bool exists = obj.Properties.ContainsKey(key.Value);
                return BoolValue.Create(exists);
            }));*/

            // keys
            scope.Define("keys", new FunctionValue("keys", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("keys() 期望 1 个参数");

                if (args[0] is not ObjectValue obj)
                    throw new RuntimeException("keys() 期望对象");

                var keys = obj.Properties.Keys.Select(k => new StringValue(k)).Cast<Value>().ToList();
                return new ArrayValue(keys);
            }));

            // bool
            scope.Define("bool", new FunctionValue("bool", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("bool() 期望 1 个参数");

                return args[0] switch
                {
                    NumberValue<int> n_int32 => BoolValue.Create(n_int32.Value > 0),
                    StringValue s => BoolValue.Create(bool.TryParse(s.Value, out var value)),
                    BoolValue b => b,
                    _ => throw new RuntimeException("bool() 无法转换该值")
                };
            }));

            // int
            scope.Define("int", new FunctionValue("int", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("int() 期望 1 个参数");

                return args[0] switch
                {
                    NumberValue<int> number_int32 => number_int32,
                    NumberValue<double> number_double => NumberValue<int>.Create((int)number_double.Value),
                    StringValue s => NumberValue<int>.Create(int.TryParse(s.Value, out var value) ? value : 0),
                    BoolValue b => NumberValue<int>.Create(b.Value ? 1 : 0),
                    _ => throw new RuntimeException("int() 无法转换该值")
                };
            }));

            // double
            scope.Define("double", new FunctionValue("double", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("double() 期望 1 个参数");

                return args[0] switch
                {
                    NumberValue<int> number_int32 => NumberValue<double>.Create(number_int32.Value),
                    NumberValue<double> number_double => number_double,
                    StringValue s => NumberValue<double>.Create(double.Parse(s.Value)),
                    BoolValue b => NumberValue<double>.Create(b.Value ? 1 : 0),
                    _ => throw new RuntimeException("double() 无法转换该值")
                };
            }));

            // str
            scope.Define("str", new FunctionValue("str", args =>
            {
                if (args.Count != 1)
                    throw new RuntimeException("str() 期望 1 个参数");

                return args[0] switch
                {
                    NumberValue<int> number_int32 => new StringValue(number_int32.Value.ToString()),
                    NumberValue<double> number_double => new StringValue(number_double.Value.ToString()),
                    StringValue s => new StringValue(s.Value),
                    BoolValue b => new StringValue(b.Value ? Boolean.TrueString : Boolean.FalseString),
                    _ => throw new RuntimeException("str() 无法转换该值")
                };
            }));
        }
    }
}