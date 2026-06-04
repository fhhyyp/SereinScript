using ScriptLang.Utils;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace ScriptLang.Runtime;



/// <summary>
/// 运行时值类型（统一类型系统）
/// </summary>
public abstract record Value
{
    public static readonly Value Null = new NullValue();

    public abstract T As<T>();
    
    public bool IsNull => this is NullValue;
    //[Obsolete("temp", true)]
    public bool IsNumber => IsNumber_Decimal  || IsNumber_Double  || IsNumber_Float || IsNumber_Long || IsNumber_Int;
    public bool IsNumber_Decimal => this is NumberValue<decimal>;
    public bool IsNumber_Double => this is NumberValue<double>;
    public bool IsNumber_Float => this is NumberValue<float>;
    public bool IsNumber_Long => this is NumberValue<long>;
    public bool IsNumber_Int => this is NumberValue<int>;


    public bool IsString => this is StringValue;
    public bool IsBool => this is BoolValue;
    public bool IsObject => this is ObjectValue;
    public bool IsArray => this is ArrayValue;
    public bool IsFunction => this is FunctionValue;
    public bool IsClrObject => this is ClrObjectValue;
    public bool IsClrMethod => this is ClrMethodValue;

    /*public T AsNumber<T>() where T : struct, IEquatable<T>, IFormattable, IConvertible
    {
        if(this is NumberValue<T> number)
        {
            return number.Value;
        }
        throw new NotImplementedException($"无法转换为 {typeof(T).Name} 类型");
    }*/

    public NumberValue<T> AsNumberValue<T>() where T : struct, IEquatable<T>, IFormattable, IConvertible
    {
        if(this is NumberValue<T> number)
        {
            return number;
        }
        throw new NotImplementedException($"无法转换为 NumberValue<{typeof(T).Name}> 类型");
    }

    public string AsString() => (this as StringValue)?.Value  ?? this.ToString();
    public bool AsBool() => (this as BoolValue)?.Value  ?? false;
    public Dictionary<string, Value> AsObject() => (this as ObjectValue)?.Properties  ?? [];
    public List<Value> AsArray() => (this as ArrayValue)?.Elements  ?? [];
    
    public sealed override string ToString()
    {
        return this switch
        {
            ArrayValue a => "[" + string.Join(", ", a.Elements) + "]",
            BoolValue b => b.Value ? "true" : "false",
            ClrMethodValue cm => $"<clr:func>{cm.Delegate.MethodInfo.DeclaringType?.Name}.{cm.Delegate.MethodInfo.Name}()",
            ClrObjectValue co => $"<clr:obj>{co.Value?.GetType().FullName}",
            FunctionValue f => $"<func>({string.Join(',', f.Parameters)}) = {{}}",
            NullValue => "null",
            NumberValue<int> n => n.Value.ToString(CultureInfo.InvariantCulture),
            NumberValue<long> n => n.Value.ToString(CultureInfo.InvariantCulture),
            NumberValue<float> n => n.Value.ToString(CultureInfo.InvariantCulture),
            NumberValue<double> n => n.Value.ToString(CultureInfo.InvariantCulture),
            NumberValue<decimal> n => n.Value.ToString(CultureInfo.InvariantCulture),
            ObjectValue o => "{" + string.Join(", ", o.Properties.Select(kv => $"{kv.Key}: {kv.Value}")) + "}",
            StringValue s => $"\"{s.Value}\"",
            _ => "unknown"
        };
    }


}

/// <summary>
/// Null 值
/// </summary>
public record NullValue : Value
{
    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(NullValue)) return (T)(object)this;
        throw new InvalidCastException($"Cannot cast NullValue to {typeof(T)}");
    }
}

public static class NumberValueCache
{
    // 缓存常用整数（-128 到 127）
    public static readonly NumberValue<int>[] SmallIntegerCache;

    // 特殊值单例
    public static readonly NumberValue<double> NaN = new NumberValue<double>(double.NaN);
    public static readonly NumberValue<double> PositiveInfinity = new NumberValue<double>(double.PositiveInfinity);
    public static readonly NumberValue<double> NegativeInfinity = new NumberValue<double>(double.NegativeInfinity);

    public static readonly NumberValue<int> Int32_M1;
    public static readonly NumberValue<int> Int32_0;
    public static readonly NumberValue<int> Int32_1;


    static NumberValueCache()
    {
        // 初始化小整数缓存
        SmallIntegerCache = new NumberValue<int>[256];
        for (int i = -128; i <= 127; i++)
        {
            SmallIntegerCache[i + 128] = new NumberValue<int>(i);
        }
        Int32_M1 = NumberValueFactory.Create(-1);
        Int32_0 = NumberValueFactory.Create(0);
        Int32_1 = NumberValueFactory.Create(1);
    }
}
public static class NumberValueFactory
{
    public static NumberValue<T> Create<T>(T value) where T : struct, IEquatable<T>, IFormattable, IConvertible
    {
        return NumberValue<T>.Create(value);
    }
}

/// <summary>
/// 数字值
/// </summary>
public record NumberValue<TNumber>(TNumber Value) : Value where TNumber : struct, IEquatable<TNumber>, IFormattable, IConvertible
{
    /// <summary>
    /// 工厂方法 - 使用缓存
    /// </summary>
    public static NumberValue<TNumber> Create(TNumber value)
    {
        // 检查特殊值
        if (value is int @int)
        {
            // 检查小整数缓存
            if (@int >= -128 && @int <= 127)
            {
                var result = NumberValueCache.SmallIntegerCache[@int + 128];
                return (NumberValue<TNumber>)(object)result;
            }
        }
        else if (value is double @double)
        {
            if (double.IsNaN(@double)) return (NumberValue<TNumber>)(object)NumberValueCache.NaN;
            if (double.IsPositiveInfinity(@double)) return (NumberValue<TNumber>)(object)NumberValueCache.PositiveInfinity;
            if (double.IsNegativeInfinity(@double)) return (NumberValue<TNumber>)(object)NumberValueCache.NegativeInfinity;
        }

        // 创建新实例
        return new NumberValue<TNumber>(value);
    }

    public override T As<T>()
    {
        if (Value is T resultValue)
        {
            return resultValue;
        }
        var targetType = typeof(T);
        if (targetType == typeof(string) && Value.ToString() is T r_string)
        {
            return r_string;
        }

        var result = targetType switch
        {
            //Type t when t == typeof(byte) && Convert.ToByte(Value) is T r => r,
            //Type t when t == typeof(short) && Convert.ToInt16(Value) is T r => r,
            Type t when t == typeof(int) && Convert.ToInt32(Value) is T r => r,
            Type t when t == typeof(long) && Convert.ToInt64(Value) is T r => r,

            Type t when t == typeof(float) && Convert.ToSingle(Value) is T r => r,
            Type t when t == typeof(double) && Convert.ToDouble(Value) is T r => r,
            Type t when t == typeof(decimal) && Convert.ToDecimal(Value) is T r => r,

            _ => throw new InvalidCastException($" '{Value}' 无法转换为 '{targetType.Name}' 类型")
        };
        return result;
    }

}

/// <summary>
/// 字符串值
/// </summary>
public record StringValue(string Value) : Value
{
    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(StringValue)) return (T)(object)this;
        if (typeof(T) == typeof(string)) return (T)(object)Value;
        throw new InvalidCastException($"Cannot cast StringValue to {typeof(T)}");
    }
}

/// <summary>
/// 布尔值
/// </summary>
public record BoolValue : Value
{
    public bool Value { get; private set; }

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(BoolValue)) return (T)(object)this;
        throw new InvalidCastException($"Cannot cast BoolValue to {typeof(T)}");
    }

    public static readonly BoolValue True;
    public static readonly BoolValue False;

    static BoolValue()
    {
        True = new BoolValue(true);
        False = new BoolValue(false);
    }

    private BoolValue(bool value)
    {
        Value = value;
    }

    //[MethodImplAttribute(MethodImplOptions.InternalCall)]
    public static BoolValue Create(bool value) => value ? True : False;
}

/// <summary>
/// 对象值（map/record）
/// </summary>
public record ObjectValue(Dictionary<string, Value> Properties) : Value
{
    public void Set(string key, Value value) => Properties[key] = value;

    public Value Get(string key) => Properties[key];

    public bool TryGetValue(string key, [NotNullWhen(true)]out Value? value)
    {
        var state = Properties.TryGetValue(key, out value);
        return state;
    }

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(ObjectValue)) return (T)(object)this;
        throw new InvalidCastException($"Cannot cast ObjectValue to {typeof(T)}");
    }
}



/// <summary>
/// 数组值
/// </summary>
public record ArrayValue(List<Value> Elements) : Value //, IObservableValue
{
    public int Length => Elements.Count;

    public List<Value> Value => Elements;

    public void Add(Value v)
    {
        Elements.Add(v);
    }

    public Value Pop()
    {
        if (Elements.Count == 0) return ScriptLang.Runtime.Value.Null;
        var last = Elements[^1];
        Elements.RemoveAt(Elements.Count - 1);
        return last;
    }

    public void RemoveAt(int index)
    {
        Elements.RemoveAt(index);
    }

    public bool Remove(Value v)
    {
        return Elements.Remove(v);
    }

    public void Set(int index, Value v)
    {
        Elements[index] = v;
    }

    public void Reverse()
    {
        Elements.Reverse();
    }

    public Value Get(int index)
    {
        var value = Elements[index];
        return value;
    }
    
    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(ArrayValue)) return (T)(object)this;
        throw new InvalidCastException($"Cannot cast ArrayValue to {typeof(T)}");
    }

}

/// <summary>
/// CLR 对象值（包装任意 C# 对象）
/// </summary>
public record ClrObjectValue(object? Target) : Value
{
    /// <summary>
    /// 获取包装的 C# 对象
    /// </summary>
    public object? Value { get; } = Target; //?? throw new ArgumentNullException(nameof(Target));


    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(ClrObjectValue)) return (T)(object)this;
        if (Value is T typed) return typed;
        throw new InvalidCastException($"Cannot cast ClrObjectValue({Value?.GetType()}) to {typeof(T)}");
    }

    /// <summary>
    /// 获取对象的属性值（用于 ToString/Debug）
    /// </summary>
    public Dictionary<string, object> GetDebugProperties()
    {
        var result = new Dictionary<string, object>();
        var properties = Value?.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ?? [];
        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(Value);
                result[prop.Name] = value ?? "null";
            }
            catch
            {
                result[prop.Name] = "<error>";
            }
        }
        return result;
    }
}

/// <summary>
/// CLR 方法值（表示可被脚本调用的 C# 方法）
/// </summary>
public record ClrMethodValue : Value
{

    public ClrMethodValue(MethodInfo methodInfo)
    {
        DelegateDetails delegateDetails = new DelegateDetails(methodInfo);
        ParameterCount = methodInfo.GetParameters().Length;
        IsStatic = methodInfo.IsStatic;
        ReturnType = methodInfo.ReturnType;
        Delegate = delegateDetails;
        //_methodInfo = methodInfo;
    }

    /// <summary>
    /// 包装的 MethodInfo
    /// </summary>
    public DelegateDetails Delegate { get; }

    /// <summary>
    /// 目标对象（实例方法需要，静态方法为 null）
    /// </summary>
    public object? TargetInstance { get; init; }

    /// <summary>
    /// 参数数量
    /// </summary>
    public int ParameterCount { get; }

    /// <summary>
    /// 是否是静态方法
    /// </summary>
    public bool IsStatic { get; }

    /// <summary>
    /// 返回类型
    /// </summary>
    public System.Type ReturnType { get; }

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(ClrMethodValue)) return (T)(object)this;
        throw new InvalidCastException($"Cannot cast ClrMethodValue to {typeof(T)}");
    }

    internal async Task<object?> InvokeAsync(object?[]? args = null)
    {
        return await Delegate.InvokeAsync(TargetInstance, args);
    }
}
