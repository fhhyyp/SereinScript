using ScriptLang.Utils;
using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ScriptLang.Runtime;


#region 可观擦能力
public interface IObservableValue
{
    event Action<ValueChange> Changed;
}
public record ValueChange(
    Value Source,
    string? Key,      // 对象属性名 / 数组索引
    Value? OldValue,
    Value? NewValue,
    ChangeType Type
);

public enum ChangeType
{
    Set,
    Add,
    Remove
} 
#endregion

/// <summary>
/// 运行时值类型（统一类型系统）
/// </summary>
public abstract record Value
{
    /// <summary>
    /// 对象与数组的值的来源
    /// </summary>
    public Value? Source { get; set; }
    /// <summary>
    /// 值在源中的标记名称
    /// </summary>
    public string? TargetKey { get; set; }
    /// <summary>
    /// 值在源中的索引
    /// </summary>
    public int TargetIndex { get; set; } = -1;

    public static readonly Value Null = new NullValue();

    public abstract T As<T>();
    
    public bool IsNull => this is NullValue || (this is MemberValue member && member.Value is NullValue);
    public bool IsNumber => this is NumberValue || (this is MemberValue member && member.Value is NumberValue);
    public bool IsString => this is StringValue || (this is MemberValue member && member.Value is StringValue);
    public bool IsBool => this is BoolValue || (this is MemberValue member && member.Value is BoolValue);
    public bool IsObject => this is ObjectValue || (this is MemberValue member && member.Value is ObjectValue);
    public bool IsArray => this is ArrayValue || (this is MemberValue member && member.Value is ArrayValue);
    public bool IsFunction => this is FunctionValue || (this is MemberValue member && member.Value is FunctionValue);
    public bool IsClrObject => this is ClrObjectValue || (this is MemberValue member && member.Value is ClrObjectValue);
    public bool IsClrMethod => this is ClrMethodValue || (this is MemberValue member && member.Value is ClrMethodValue);
    
    public double AsNumber() => (this as NumberValue)?.Value ?? ((this as MemberValue)?.Value)?.AsNumber() ?? 0;
    public string AsString() => (this as StringValue)?.Value ??  ((this as MemberValue)?.Value)?.AsString() ?? this.ToString();
    public bool AsBool() => (this as BoolValue)?.Value ?? ((this as MemberValue)?.Value)?.AsBool() ?? false;
    public Dictionary<string, MemberValue> AsObject() => (this as ObjectValue)?.Properties ?? ((this as MemberValue)?.Value)?.AsObject() ?? new();
    public List<Value> AsArray() => (this as ArrayValue)?.Elements ?? ((this as MemberValue)?.Value)?.AsArray() ?? new();
    
    public sealed override string ToString()
    {
        return this switch
        {
            NullValue => "null",
            NumberValue n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StringValue s => $"\"{s.Value}\"",
            BoolValue b => b.Value ? "true" : "false",
            MemberValue m => $"<menber:({m.Value.GetType().Name}){m.Value}>",
            ObjectValue o => "{" + string.Join(", ", o.Properties.Select(kv => $"{kv.Key}: {kv.Value}")) + "}",
            ArrayValue a => "[" + string.Join(", ", a.Elements) + "]",
            FunctionValue f => $"<function:{string.Join(',', f.Parameters)}>",
            ClrObjectValue co => $"<CLR:{co.Target?.GetType().Name}>",
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
/// 对象成员
/// </summary>
/// <param name="MemberName">值名称</param>
/// <param name="Value">值内容</param>
/// <param name="Source">值来源</param>
public record MemberValue(string MemberName, Value Value) : Value
{
    public override T As<T>() => Value.As<T>();
}

/// <summary>
/// 对象值（map/record）
/// </summary>
public record ObjectValue(Dictionary<string, MemberValue> Properties) : Value, IObservableValue
{
    public event Action<ValueChange>? Changed;

    public void Set(string key, Value value)
    {
        var menberValue = new MemberValue(key, value);
        if(menberValue.Source is null)
        {
            menberValue.Source = this;
            menberValue.TargetKey = key;
        }
        

        Properties.TryGetValue(key, out var old);
        Properties[key] = menberValue;
        Changed?.Invoke(new ValueChange(
            this, key, old, value, ChangeType.Set
        ));
    }

    public MemberValue Get(string key)
    {
        return Properties[key];
    }

    public bool TryGetValue(string key, [NotNullWhen(true)]out MemberValue? value)
    {
        var state = Properties.TryGetValue(key, out value);
        if (value?.Value.Source is null)
        {
            value?.Value.TargetKey = key;
            value?.Value.Source = this;
        }
        return state;
    }

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
public record ArrayValue(List<Value> Elements) : Value, IObservableValue
{
    public event Action<ValueChange>? Changed;

    public int Length => Elements.Count;

    private readonly List<FunctionValue> OnChangeds = new List<FunctionValue>();

    internal void AddOnChanged(FunctionValue functionValue)
    {
        OnChangeds.Add(functionValue);
    }

    private void PublicEvent(ValueChange e, ScriptEngine engine)
    {
        Changed?.Invoke(e);
        if(e.Type is ChangeType.Set or ChangeType.Add or ChangeType.Remove)
        {
            var ee = new ValueChange(this, nameof(Length), null, GetLength(), ChangeType.Set);
            Changed?.Invoke(ee);
        }
        foreach (var item in OnChangeds)
        {
            //_ = item.CallAsync(engine, this, e.Source, e.OldValue ?? Value.Null, e.NewValue ?? Value.Null);
            _ = item.CallAsync(engine, this);
        }
    }

    private void Track()
    {
        foreach(var (item, index) in Elements.Select((x, i) => (x, i)))
        {
            item.TargetIndex = index;
        }
    }

    public void Add(Value v, ScriptEngine engine)
    {
        Elements.Add(v);
        Track();
        PublicEvent(new ValueChange( 
            this, (Elements.Count - 1).ToString(), null, v, ChangeType.Add), engine);
    }

    public Value Pop(ScriptEngine engine)
    {
        if (Elements.Count == 0) return Value.Null;
        var last = Elements[^1];
        Elements.RemoveAt(Elements.Count - 1);
        Track();
        PublicEvent(new ValueChange(
            this, null, last, null, ChangeType.Remove), engine);
        return last;
    }

    public void RemoveAt(int index, ScriptEngine engine)
    {

        var old = Elements[index];
        Elements.RemoveAt(index);
        Track();
        PublicEvent(new ValueChange(
            this, index.ToString(), old, null, ChangeType.Remove), engine);
    }

    public bool Remove(Value v, ScriptEngine engine)
    {
        var idx = Elements.IndexOf(v);
        if (idx < 0 && idx >= Elements.Count) return false;
        RemoveAt(idx, engine);
        return true;
    }

    public void Set(int index, Value v, ScriptEngine engine)
    {

        var old = Elements[index];
        Elements[index] = v;
        Track();
        PublicEvent(new ValueChange(
            this, index.ToString(), old, v, ChangeType.Set), engine);
    }

    public void Reverse(ScriptEngine engine)
    {
        Elements.Reverse();
        Track();
        PublicEvent(new ValueChange(this, null, null, null, ChangeType.Set), engine);
    }
    public Value Get(int index)
    {
        var value = Elements[index];
        if(value.Source is null)
        {
            value.Source = this;
            value.TargetIndex = index;
        }
        return value;
    }
    internal NumberValue GetLength()
    {
        var value = new NumberValue(Length);
        if (value.Source is null)
        {
            value.Source = this;
            value.TargetKey = nameof(Length);
        }
        return value;
    }

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(ArrayValue)) return (T)(object)this;
        throw new InvalidCastException($"Cannot cast ArrayValue to {typeof(T)}");
    }




    /* public override bool Equals(ArrayValue? other)
     {
         foreach (var (item, index) in Elements.Select((v, i) => (v, i)))
         {
             if (!item.Equals(other?.Elements[index]))
                 return false;
         }
         return true;
     }*/

}

/// <summary>
/// CLR 对象值（包装任意 C# 对象）
/// </summary>
public record ClrObjectValue(object? Target) : Value
{
    /// <summary>
    /// 获取包装的 C# 对象
    /// </summary>
    public object? Target { get; } = Target; //?? throw new ArgumentNullException(nameof(Target));

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
