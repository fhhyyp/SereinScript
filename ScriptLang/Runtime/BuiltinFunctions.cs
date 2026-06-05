using ScriptLang.Runtime;
using System.Diagnostics;

namespace ScriptLang
{
    public static class BuiltinFunctions
    {
        private static readonly FunctionValue debug = new(nameof(debug), static async (args) =>
        {
            Console.WriteLine($"debug:: {string.Join(", ", args)}");
            return Value.Null;
        });

        private static readonly FunctionValue now = new(nameof(now), static (args) =>
        {
            return NumberValueFactory.Create(DateTime.Now.Ticks);
        });

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
                _ => "unknown"
            };

            return new StringValue(typeStr);
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

            var keys = obj.Properties.Keys.Select(k => new StringValue(k)).Cast<Value>().ToList();
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
                NumberValue<int> number_int32 => new StringValue(number_int32.Value.ToString()),
                NumberValue<double> number_double => new StringValue(number_double.Value.ToString()),
                StringValue s => new StringValue(s.Value),
                BoolValue b => new StringValue(b.Value ? Boolean.TrueString : Boolean.FalseString),
                _ => throw new RuntimeException("str() 无法转换该值")
            };
        });

        public static List<FunctionValue> FunctionCaches { get; private set; } = [
                debug,
                now,
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