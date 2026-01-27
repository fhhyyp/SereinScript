using ScriptLang.Utils;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;

namespace ScriptLang.Runtime;

/// <summary>
/// 运行时值类型（统一类型系统）
/// </summary>
public abstract record Value
{
    public static readonly Value Null = new NullValue();
    
    public abstract T As<T>();
    
    public bool IsNull => this is NullValue;
    public bool IsNumber => this is NumberValue;
    public bool IsString => this is StringValue;
    public bool IsBool => this is BoolValue;
    public bool IsObject => this is ObjectValue;
    public bool IsArray => this is ArrayValue;
    public bool IsFunction => this is FunctionValue;
    public bool IsClrObject => this is ClrObjectValue;
    public bool IsClrMethod => this is ClrMethodValue;
    
    public double AsNumber() => (this as NumberValue)?.Value ?? 0;
    public string AsString() => (this as StringValue)?.Value ?? this.ToString();
    public bool AsBool() => (this as BoolValue)?.Value ?? false;
    public Dictionary<string, Value> AsObject() => (this as ObjectValue)?.Properties ?? new();
    public List<Value> AsArray() => (this as ArrayValue)?.Elements ?? new();
    
    public sealed override string ToString()
    {
        return this switch
        {
            NullValue => "null",
            NumberValue n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StringValue s => $"\"{s.Value}\"",
            BoolValue b => b.Value ? "true" : "false",
            ObjectValue o => "{" + string.Join(", ", o.Properties.Select(kv => $"{kv.Key}: {kv.Value}")) + "}",
            ArrayValue a => "[" + string.Join(", ", a.Elements) + "]",
            FunctionValue f => $"<function:{f.ParameterCount} params>",
            ClrObjectValue co => $"<CLR:{co.Target.GetType().Name}>",
            ClrMethodValue cm => $"<CLR Method:{cm.Delegate.MethodInfo.DeclaringType?.Name}.{cm.Delegate.MethodInfo.Name}>",
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

/// <summary>
/// 数字值
/// </summary>
public record NumberValue(double Value) : Value
{
    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(NumberValue)) return (T)(object)this;
        throw new InvalidCastException($"Cannot cast NumberValue to {typeof(T)}");
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
public record BoolValue(bool Value) : Value
{
    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(BoolValue)) return (T)(object)this;
        throw new InvalidCastException($"Cannot cast BoolValue to {typeof(T)}");
    }
}

/// <summary>
/// 对象值（map/record）
/// </summary>
public record ObjectValue(Dictionary<string, Value> Properties) : Value
{
    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(ObjectValue)) return (T)(object)this;
        throw new InvalidCastException($"Cannot cast ObjectValue to {typeof(T)}");
    }

    public T Get<T>(string key)
    {
        if(Properties.TryGetValue(key, out var value))
        {
            return value.As<T>();
        }
        throw new Exception($"type required");
    }

    public bool ContainsKey(string key) => Properties.ContainsKey(key);
}

/// <summary>
/// 数组值
/// </summary>
public record ArrayValue(List<Value> Elements) : Value
{
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
    public object Target { get; } = Target ?? throw new ArgumentNullException(nameof(Target));

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(ClrObjectValue)) return (T)(object)this;
        if (Target is T typed) return typed;
        throw new InvalidCastException($"Cannot cast ClrObjectValue({Target.GetType()}) to {typeof(T)}");
    }

    /// <summary>
    /// 获取对象的属性值（用于 ToString/Debug）
    /// </summary>
    public Dictionary<string, object> GetDebugProperties()
    {
        var result = new Dictionary<string, object>();
        var properties = Target.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(Target);
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
