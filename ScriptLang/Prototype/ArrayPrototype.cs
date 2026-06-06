using ScriptLang;
using ScriptLang.Runtime;

namespace ScriptLang.Prototype
{
    /// <summary>
    /// 数组原型拓展方法
    /// </summary>

    [PrototypeExtension(PushThis = true, NamingFormat = NamingFormat.Js)]
    public partial class ArrayPrototype
    {
        public partial bool IsTarget(Value value)
        {
            return value is ArrayValue;
        }

        [PrototypeProperty]
        private static NumberValue<int> Count(ArrayValue array)
        {
            return NumberValueFactory.Create(array.Elements.Count);
        }

        [PrototypeProperty]
        private static NumberValue<int> Length(ArrayValue array)
        {
            return NumberValueFactory.Create(array.Elements.Count);
        }

        [PrototypeFunction]
        private static void Add(ArrayValue array, Value item)
        {
            array.Add(item);
        }


        [PrototypeFunction]
        private static Value First(ArrayValue array)
        {
            return array.Elements.Count > 0 ? array.Elements[0] : Value.Null;
        }

        [PrototypeFunction]
        private static Value Last(ArrayValue array)
        {
            return array.Elements.Count > 0 ? array.Elements[^1] : Value.Null;
        }


        [PrototypeFunction]
        private static async ValueTask<ArrayValue> Select(ArrayValue array, ICallable func, ScriptEngine engine)
        {
            if (func == null)
                throw new RuntimeException("select() 期望一个函数");

            var result = new List<Value>();
            foreach (var item in array.Elements)
                result.Add(await func.CallAsync(engine, item));
            return new ArrayValue(result);
        }

        [PrototypeFunction]
        private static async ValueTask<ArrayValue> Where(ArrayValue array, ICallable func, ScriptEngine engine)
        {
            if (func == null)
                throw new RuntimeException("where() 期望一个函数");

            var result = new List<Value>();
            foreach (var item in array.Elements)
            {
                var r = await func.CallAsync(engine, item);

                //var r = await func.CallAsync(engine, item);
                if (IsTrue(r))
                    result.Add(item);
            }
            return new ArrayValue(result);
        }

        [PrototypeFunction]
        private static ArrayValue OrderBy(ArrayValue array)
        {
            return OrderByImpl(array, true);
        }

        [PrototypeFunction]
        private static ArrayValue OrderByDesc(ArrayValue array)
        {
            return OrderByImpl(array, false);
        }

        [PrototypeFunction]
        private static ArrayValue ToList(ArrayValue array)
        {
            return new ArrayValue(array.Elements);
        }

        // ==================== 辅助方法 ====================

        private static bool IsTrue(Value v)
        {
            return v is not NullValue and not BoolValue { Value: false };
        }

        private static ArrayValue OrderByImpl(ArrayValue arr, bool ascending)
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

}
