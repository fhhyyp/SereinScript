using System.Diagnostics;
using System.Runtime.Serialization;
using ScriptLang.Runtime;

namespace ScriptLang
{
    internal static class TryCall
    {
        private static ObjectValue Result(bool ok, Value? result = null, string? message = null, string? stack = null)
        {
            var propertys = new Dictionary<string, Value>(4)
            {
                {nameof(ok), BoolValue.Create(ok)},
                {nameof(result), result ?? Value.Null},
                {nameof(message), StringValue.Create(message)},
                {nameof(stack), StringValue.Create(stack)},
            };
            return new ObjectValue(propertys);
        }

        internal static ObjectValue Succeed(Value value) => Result(false, value);
        internal static ObjectValue Error(Exception ex) => Result(false, message: ex.Message, stack: ex.StackTrace);
    }

    public static class BuiltinFunctions
    {
        private static readonly FunctionValue debug = new(nameof(debug), static async (args) =>
        {
            throw new Exception("测试抛出异常");
            Console.WriteLine($"debug:: {string.Join(", ", args)}");
            return Value.Null;
        });

        private static readonly FunctionValue now = new(nameof(now), static (args) =>
        {
            return NumberValueFactory.Create(DateTime.Now.Ticks);
        });

        private static readonly FunctionValue tryCall = new(nameof(tryCall), static async (env, args) =>
        {
            if (args.Count is < 1 or > 3)
                throw new RuntimeException("tryCall() 期望 1 个或 2 个或 3 个参数");

            if (args[0] is not ICallable callable)
                throw new RuntimeException("tryCall() 第 1 个参数期望 FunctionValue");

            try
            {
                var result = await callable.CallAsync(env);
                return TryCall.Succeed(result);
            }
            catch (Exception ex)
            {
                // 处理错误回调（如果有第2个参数）
                if (args.Count == 2)
                {
                    if (args[1] is ICallable catchBackcall)
                    {
                        await TryErrorCallback(catchBackcall, env, ex);
                    }
                    else
                    {
                        throw new RuntimeException("tryCall() 第 2 个参数期望 FunctionValue");
                    }
                }

                return TryCall.Error(ex);
            }
            finally
            {
                // 处理错误回调（如果有第2个参数）
                if (args.Count == 3)
                {
                    if (args[2] is ICallable finallyBackcall)
                    {
                        await FinallyCallback(finallyBackcall, env);
                    }
                    else
                    {
                        throw new RuntimeException("tryCall() 第 3 个参数期望 FunctionValue");
                    }
                }
            }
            static async Task TryErrorCallback(ICallable callback, ScriptEngine env, Exception ex)
            {
                try
                {
                    await callback.CallAsync(env, new ClrObjectValue(ex));
                }
                catch (Exception callbackEx)
                {
                    Console.WriteLine($"处理 'tryCall' catch 时发生异常：{callbackEx}");
                }
            }
            static async Task FinallyCallback(ICallable callback, ScriptEngine env)
            {
                try
                {
                    await callback.CallAsync(env);
                }
                catch (Exception callbackEx)
                {
                    Console.WriteLine($"处理 'tryCall' finally 时发生异常：{callbackEx}");
                }
            }
        });

        /*private static readonly FunctionValue nowtime = new(nameof(nowtime), static (args) =>
        {
            return StringValue.Create(DateTime.Now.ToString());
        });*/

        private static readonly FunctionValue sleep = new(nameof(sleep), static async (args) =>
        {
            if (args.Count != 1 || !args[0].IsNumber_Int)
                throw new RuntimeException("sleep() 期望 1 个参数");

            var index = args[0].As<int>();
            await Task.Delay(index);
            return Value.Null;
        });

        private static readonly FunctionValue @typeof = new(nameof(@typeof), static (args) =>
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
                ClrObjectValue => "clrObject",
                ClrMethodValue => "clrMethod",
                CompiledFunctionValue => "compiledFunction",
                MutableNumber => "mutableNumber",
                RangeIterator => "rangeIterator",
                _ => "unknown"
            };

            return StringValue.Create(typeStr);
        });

        private static readonly FunctionValue print = new(nameof(print), static args =>
        {
            Console.Write("[Console]");
            foreach (var arg in args)
            {
                Console.Write(arg.AsString() + " ");
                Debug.Write(arg.AsString() + " ");
            }
            Console.WriteLine();
            return Value.Null;
        });

        private static readonly FunctionValue range = new(nameof(range), static args =>
        {
            if (args.Count == 1)
            {
                int end = args[0].As<int>();
                return new RangeIterator(0, end);
            }
            else if (args.Count == 2)
            {
                int start = args[0].As<int>();
                int end = args[1].As<int>();
                return new RangeIterator(start, end);
            }
            throw new RuntimeException("range() 期望 1 或 2 个参数");
        });

        private static readonly FunctionValue len = new(nameof(len), static args =>
        {
            if (args.Count != 1)
                throw new RuntimeException("len() 期望 1 个参数");
            var lenValue = args[0] switch
            {
                StringValue s => NumberValueFactory.Create(s.Value.Length),
                ArrayValue a => NumberValueFactory.Create(a.Length),
                _ => throw new RuntimeException("len() 期望字符串或数组")
            };
            return lenValue;
        });

        private static readonly FunctionValue keys = new(nameof(keys), static args =>
        {
            if (args.Count != 1)
                throw new RuntimeException("keys() 期望 1 个参数");

            if (args[0] is not ObjectValue obj)
                throw new RuntimeException("keys() 期望对象");

            var keys = obj.Properties.Keys.Select(k => StringValue.Create(k)).Cast<Value>().ToList();
            return new ArrayValue(keys);
        });

        private static readonly FunctionValue @bool = new(nameof(@bool), static args =>
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
        });

        private static readonly FunctionValue @int = new(nameof(@int), static args =>
        {
            if (args.Count != 1)
                throw new RuntimeException("int() 期望 1 个参数");

            return args[0] switch
            {
                NumberValue<int> number_int32 => number_int32,
                NumberValue<double> number_double => NumberValueFactory.Create((int)number_double.Value),
                StringValue s => NumberValueFactory.Create(int.TryParse(s.Value, out var value) ? value : 0),
                BoolValue b => NumberValueFactory.Create(b.Value ? 1 : 0),
                _ => throw new RuntimeException("int() 无法转换该值")
            };
        });

        private static readonly FunctionValue @double = new(nameof(@double), static args =>
        {
            if (args.Count != 1)
                throw new RuntimeException("double() 期望 1 个参数");

            return args[0] switch
            {
                NumberValue<int> number_int32 => NumberValueFactory.Create(number_int32.Value),
                NumberValue<double> number_double => number_double,
                StringValue s => NumberValueFactory.Create(double.Parse(s.Value)),
                BoolValue b => NumberValueFactory.Create(b.Value ? 1 : 0),
                _ => throw new RuntimeException("double() 无法转换该值")
            };
        });

        private static readonly FunctionValue str = new(nameof(str), static args =>
        {
            if (args.Count != 1)
                throw new RuntimeException("str() 期望 1 个参数");

            return args[0] switch
            {
                NumberValue<int> number_int32 => StringValue.Create(number_int32.Value.ToString()),
                NumberValue<double> number_double => StringValue.Create(number_double.Value.ToString()),
                StringValue s => StringValue.Create(s.Value),
                BoolValue b => StringValue.Create(b.Value ? Boolean.TrueString : Boolean.FalseString),
                _ => throw new RuntimeException("str() 无法转换该值")
            };
        });

        public static List<FunctionValue> FunctionCaches { get; private set; } = [
                debug,
                now,
                tryCall,
                sleep,
                @typeof,
                print,
                range,
                len,
                keys,
                @bool,
                @int,
                @double,
                str,
            ];

        public static void RegisterAll(Scope scope)
        {
            foreach (var item in FunctionCaches)
            {
                scope.DefineFunction(item);
            }

        }
    }
}