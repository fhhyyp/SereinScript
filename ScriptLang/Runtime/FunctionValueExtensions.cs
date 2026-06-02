using ScriptLang.Parser;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ScriptLang.Runtime;

/// <summary>
/// FunctionValue 扩展：转换为 CLR 委托
/// </summary>
public static class FunctionValueExtensions
{
    public static Func<Value[], Task<Value>> ToDelegate(this FunctionValue function, ScriptEngine engine, Scope? parentScope = null)
    {
        if (function.IsNative)
        {
            return async args => function.NativeFunc!(args.ToList());
        }

        var closure = function.Closure;
        var body = function.Body;
        var paramCount = function.ParameterCount;

        return async args =>
        {
            if (args.Length != paramCount)
                throw new RuntimeException($"函数期望 {paramCount} 个参数，但实际传入 {args.Length} 个");

            // 使用传入 ScriptEngine 的统一 Interpreter
            var interpreter = new Interpreter(engine);

            // 创建调用作用域（闭包作为父作用域）
            var callScope = new Scope(closure); // closure.CreateChildScope(); // new Scope(closure);

            for (int i = 0; i < function.Parameters.Count; i++)
            {
                callScope.Define(function.Parameters[i], args[i]);
            }

            var result = await interpreter.EvaluateAsync(body, callScope);
            
            return result.Value;
        };
    }

    /// <summary>
    /// 将 FunctionValue 转换为 0 参数委托
    /// </summary>
    public static Func<Task<Value>> ToDelegate0(this FunctionValue function, ScriptEngine engine, Scope? parentScope = null)
    {
        var @delegate = function.ToDelegate(engine, parentScope);
        return () => @delegate(Array.Empty<Value>());
    }

    /// <summary>
    /// 将 FunctionValue 转换为 1 参数委托
    /// </summary>
    public static Func<Value, Task<Value>> ToDelegate1(this FunctionValue function, ScriptEngine engine, Scope? parentScope = null)
    {
        var @delegate = function.ToDelegate(engine, parentScope);
        return arg => @delegate(new[] { arg });
    }

    /// <summary>
    /// 将 FunctionValue 转换为 2 参数委托
    /// </summary>
    public static Func<Value, Value, Task<Value>> ToDelegate2(this FunctionValue function, ScriptEngine engine, Scope? parentScope = null)
    {
        var @delegate = function.ToDelegate(engine, parentScope);
        return (arg1, arg2) => @delegate(new[] { arg1, arg2 });
    }

    /// <summary>
    /// 将 FunctionValue 转换为 3 参数委托
    /// </summary>
    public static Func<Value, Value, Value, Task<Value>> ToDelegate3(this FunctionValue function, ScriptEngine engine, Scope? parentScope = null)
    {
        var @delegate = function.ToDelegate(engine, parentScope);
        return (arg1, arg2, arg3) => @delegate(new[] { arg1, arg2, arg3 });
    }

    /// <summary>
    /// 自动根据参数数量选择合适的委托类型
    /// </summary>
    public static Delegate ToAutoDelegate(this FunctionValue function, ScriptEngine engine, Scope? parentScope = null)
    {
        return function.ParameterCount switch
        {
            0 => function.ToDelegate0(engine, parentScope),
            1 => function.ToDelegate1(engine, parentScope),
            2 => function.ToDelegate2(engine, parentScope),
            3 => function.ToDelegate3(engine, parentScope),
            _ => function.ToDelegate(engine, parentScope)
        };
    }

    /// <summary>
    /// 将 FunctionValue 转换为泛型委托（支持 Value 到 CLR 类型的自动转换）
    /// </summary>
    public static TDelegate ToGenericDelegate<TDelegate>(this FunctionValue function, ScriptEngine engine, Scope? parentScope = null) where TDelegate : System.Delegate
    {
        var @delegate = function.ToDelegate(engine, parentScope);

        // 创建泛型适配器
        return (TDelegate)FunctionValueAdapter.CreateAdapter<TDelegate>(@delegate, function.ParameterCount);
    }
}

/// <summary>
/// 泛型委托适配器工厂
/// </summary>
public static class FunctionValueAdapter
{
    /// <summary>
    /// 创建适配器：将 Func<Value[], Task<Value>> 适配为目标委托类型
    /// </summary>
    public static Delegate CreateAdapter<TDelegate>(Func<Value[], Task<Value>> @delegate, int paramCount)
    {
        var delegateType = typeof(TDelegate);
        var invokeMethod = delegateType.GetMethod("Invoke");

        if (invokeMethod == null)
            throw new ArgumentException("目标类型必须是委托", nameof(TDelegate));

        // 创建适配器闭包
        return CreateAdapter(@delegate, invokeMethod, paramCount);
    }

    private static Delegate CreateAdapter(Func<Value[], Task<Value>> @delegate, System.Reflection.MethodInfo invokeMethod, int paramCount)
    {
        var paramTypes = invokeMethod.GetParameters().Select(p => p.ParameterType).ToArray();
        var returnType = invokeMethod.ReturnType;

        // 使用反射创建适配器
        var method = typeof(FunctionValueAdapter)
            .GetMethod(nameof(CreateAdapterCore), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        if (paramTypes.Length == 0)
        {
            method = method.MakeGenericMethod(returnType);
        }
        else if (paramTypes.Length == 1)
        {
            method = method.MakeGenericMethod(returnType, paramTypes[0]);
        }
        else if (paramTypes.Length == 2)
        {
            method = method.MakeGenericMethod(returnType, paramTypes[0], paramTypes[1]);
        }
        else if (paramTypes.Length == 3)
        {
            method = method.MakeGenericMethod(returnType, paramTypes[0], paramTypes[1], paramTypes[2]);
        }
        else
        {
            throw new ArgumentException("泛型适配器仅支持 0-3 个参数", "delegate");
        }

        return (Delegate)method.Invoke(null, new object[] { @delegate, paramCount })!;
    }

    private static Delegate CreateAdapterCore<TReturn, T1>(
        Func<Value[], Task<Value>> @delegate, int paramCount)
    {
        var adapter = (Func<T1, Task<TReturn>>)(arg =>
        {
            var args = new Value[] { ConvertToValue(arg) };
            var task = @delegate(args);
            return ConvertResult<TReturn>(task);
        });
        return adapter;
    }

    private static Delegate CreateAdapterCore<TReturn, T1, T2>(
        Func<Value[], Task<Value>> @delegate, int paramCount)
    {
        var adapter = (Func<T1, T2, Task<TReturn>>)((arg1, arg2) =>
        {
            var args = new Value[] { ConvertToValue(arg1), ConvertToValue(arg2) };
            var task = @delegate(args);
            return ConvertResult<TReturn>(task);
        });
        return adapter;
    }

    private static Delegate CreateAdapterCore<TReturn, T1, T2, T3>(
        Func<Value[], Task<Value>> @delegate, int paramCount)
    {
        var adapter = (Func<T1, T2, T3, Task<TReturn>>)((arg1, arg2, arg3) =>
        {
            var args = new Value[] { ConvertToValue(arg1), ConvertToValue(arg2), ConvertToValue(arg3) };
            var task = @delegate(args);
            return ConvertResult<TReturn>(task);
        });
        return adapter;
    }

    private static Delegate CreateAdapterCore<TReturn>(
        Func<Value[], Task<Value>> @delegate, int paramCount)
    {
        var adapter = new Func<Task<TReturn>>(() =>
        {
            var task = @delegate(Array.Empty<Value>());
            return ConvertResult<TReturn>(task);
        });
        return adapter;
    }

    /// <summary>
    /// 将 CLR 参数转换为 Value
    /// </summary>
    private static Value ConvertToValue(object? arg)
    {
        if (arg == null)
            return Value.Null;

        var type = arg.GetType();

        // 基本类型

        if (type == typeof(byte))
            return NumberValue<byte>.Create(Convert.ToByte(arg));
        if (type == typeof(short))
            return NumberValue<short>.Create(Convert.ToInt16(arg));
        if (type == typeof(int))
            return NumberValue<int>.Create(Convert.ToInt32(arg));
        if (type == typeof(long))
            return NumberValue<long>.Create(Convert.ToInt64(arg));
        if (type == typeof(float))
            return NumberValue<float>.Create(Convert.ToSingle(arg));
        if (type == typeof(double))
            return NumberValue<double>.Create(Convert.ToDouble(arg));
        if (type == typeof(decimal))
            return NumberValue<decimal>.Create(Convert.ToDecimal(arg));

        if (type == typeof(bool))
            return BoolValue.Create((bool)arg);
        if (type == typeof(string) || type == typeof(char))
            return new StringValue(arg.ToString()!);

        // 如果已经是 Value，直接返回
        if (arg is Value value)
            return value;

        // CLR 对象包装
        return new ClrObjectValue(arg);
    }

    /// <summary>
    /// 将 Task<Value> 结果转换为目标类型
    /// </summary>
    private static async Task<TReturn> ConvertResult<TReturn>(Task<Value> task)
    {
        var value = await task;

        // 如果目标类型就是 Value，直接返回
        if (typeof(TReturn) == typeof(Value))
            return (TReturn)(object)value;

        // 处理数值类型
        if (value.IsNumber_Int && value.As<int>() is TReturn r_int32)
        {
            return r_int32;
        }
        if (value.IsNumber_Double && value.As<double>() is TReturn r_double)
        {
            return r_double;
        }
        if (value.IsNumber_Float && value.As<float>() is TReturn r_float)
        {
            return r_float;
        }
        if (value.IsNumber_Double && value.As<long>() is TReturn r_int64)
        {
            return r_int64;
        }

        // 处理 StringValue → string
        if (value.IsString && typeof(TReturn) == typeof(string))
            return (TReturn)(object)value.AsString();

        // 处理 BoolValue → bool
        if (value.IsBool && typeof(TReturn) == typeof(bool))
            return (TReturn)(object)value.AsBool();

        // 处理 NullValue
        if (value.IsNull)
        {
            if (typeof(TReturn).IsValueType && System.Nullable.GetUnderlyingType(typeof(TReturn)) == null)
                throw new InvalidCastException($"无法将 null 转换为不可为 null 的值类型 {typeof(TReturn).Name}");
            return default!;
        }

        // 处理 ClrObjectValue → 目标类型
        if (value is ClrObjectValue clrObj && clrObj.ClrObject is not null && typeof(TReturn).IsAssignableFrom(clrObj.ClrObject.GetType()))
            return (TReturn)clrObj.ClrObject;

        throw new InvalidCastException($"无法将 Value ({value.GetType()}) 转换为 {typeof(TReturn).Name}");
    }
}
