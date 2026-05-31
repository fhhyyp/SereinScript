using ScriptLang.Parser;
using ScriptLang.Utils;
using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;

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
    
    public double AsNumber() => (this as NumberValue)?.Value  ?? 0;
    public string AsString() => (this as StringValue)?.Value  ?? this.ToString();
    public bool AsBool() => (this as BoolValue)?.Value  ?? false;
    public Dictionary<string, Value> AsObject() => (this as ObjectValue)?.Properties  ?? new();
    public List<Value> AsArray() => (this as ArrayValue)?.Elements  ?? new();
    
    public sealed override string ToString()
    {
        return this switch
        {
            ArrayValue a => "[" + string.Join(", ", a.Elements) + "]",
            BoolValue b => b.Value ? "true" : "false",
            ClrMethodValue cm => $"<clr:func>{cm.Delegate.MethodInfo.DeclaringType?.Name}.{cm.Delegate.MethodInfo.Name}()",
            ClrObjectValue co => $"<clr:obj>{co.ClrObject?.GetType().FullName}>",
            FunctionValue f => $"<func>{string.Join(',', f.Parameters)}>",
            NullValue => "null",
            NumberValue n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ObjectValue o => "{" + string.Join(", ", o.Properties.Select(kv => $"{kv.Key}: {kv.Value}")) + "}",
            StringValue s => $"\"{s.Value}\"",
            _ => "unknown"
        };
    }

    internal abstract Value Copy();
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

    internal override Value Copy() => Value.Null;
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

    internal override Value Copy() => new NumberValue(Value);
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

    internal override Value Copy() => new StringValue(Value);
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
    internal override Value Copy() => new BoolValue(Value);
}

/// <summary>
/// 对象值（map/record）
/// </summary>
public record ObjectValue(Dictionary<string, Value> Properties) : Value, IObservableValue
{
    public event Action<ValueChange>? Changed;


    internal override Value Copy()
    {
        var values = new Dictionary<string, Value>();
        foreach(var property in Properties)
        {
            var key = property.Key;
            var value = property.Value.Copy();
            values[key] = value;
        }
        return new ObjectValue(values);
    }

    public void Set(string key, Value value)
    {
        Properties.TryGetValue(key, out var old);
        Properties[key] = value;
        Changed?.Invoke(new ValueChange(
            this, key, old, value, ChangeType.Set
        ));
    }

    public Value Get(string key)
    {
        return Properties[key];
    }

    public bool TryGetValue(string key, [NotNullWhen(true)]out Value? value)
    {
        var state = Properties.TryGetValue(key, out value);
        /*if (value?.Source is null)
        {
            value?.Target = new StringValue(key);
            value?.Source = this;
        }*/
        return state;
    }

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(ObjectValue)) return (T)(object)this;
        throw new InvalidCastException($"Cannot cast ObjectValue to {typeof(T)}");
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

    internal override Value Copy()
    {
        var array = Elements.Select(x => x.Copy()).ToList();
        return new ArrayValue(array);
    }



    public void Add(Value v, ScriptEngine engine)
    {
        Elements.Add(v);
        //Track();
        PublicEvent(new ValueChange( 
            this, (Elements.Count - 1).ToString(), null, v, ChangeType.Add), engine);
    }

    public Value Pop(ScriptEngine engine)
    {
        if (Elements.Count == 0) return Value.Null;
        var last = Elements[^1];
        Elements.RemoveAt(Elements.Count - 1);
        //Track();
        PublicEvent(new ValueChange(
            this, null, last, null, ChangeType.Remove), engine);
        return last;
    }

    public void RemoveAt(int index, ScriptEngine engine)
    {

        var old = Elements[index];
        Elements.RemoveAt(index);
        //Track();
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
        //Track();
        PublicEvent(new ValueChange(
            this, index.ToString(), old, v, ChangeType.Set), engine);
    }

    public void Reverse(ScriptEngine engine)
    {
        Elements.Reverse();
        //Track();
        PublicEvent(new ValueChange(this, null, null, null, ChangeType.Set), engine);
    }
    public Value Get(NumberValue index)
    {
        var value = Elements[(int)index.Value];
        /*if(value.Source is null)
        {
            value.Source = this;
            value.Target = index;
        }*/
        return value;
    }

    public Value Get(int index)
    {
        var value = Elements[index];
        /*if (value.Source is null)
        {
            value.Source = this;
            value.Target = new NumberValue(index);
        }*/
        return value;
    }
    internal NumberValue GetLength()
    {
        var value = new NumberValue(Length);
        /*if (value.Source is null)
        {
            value.Source = this;
            value.Target = new StringValue(nameof(Length));
        }*/
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
/// 函数值（Lambda + 闭包）
/// </summary>
public record FunctionValue : Value
{
    /// <summary>
    /// 参数名列表
    /// </summary>
    public List<string> Parameters { get; }

    /// <summary>
    /// 函数体（AST 表达式）
    /// </summary>
    public Parser.Expr Body { get; }

    /// <summary>
    /// 闭包作用域（捕获定义时的环境）
    /// </summary>
    public Scope Closure { get; }

    /// <summary>
    /// 轻量级闭包（优化版本）
    /// </summary>
    public LightweightClosure? OptimizedClosure { get; private set; }


    /// <summary>
    /// 是否是原生函数
    /// </summary>
    public bool IsNative { get; }

    /// <summary>
    /// 是否为原生异步函数
    /// </summary>
    public bool IsNativeTask { get; }

    /// <summary>
    /// 原生函数委托
    /// </summary>
    public Func<List<Value>, Value>? NativeFunc { get; }

    /// <summary>
    /// 原生异步函数委托
    /// </summary>
    public Func<List<Value>, Task<Value>>? NativeTask { get; }

    /// <summary>
    /// 参数数量
    /// </summary>
    public int ParameterCount => Parameters.Count;

    /// <summary>
    /// 创建 DSL Lambda 函数（优化版本）
    /// </summary>
    public FunctionValue(LambdaExpr lambda, Scope closure)
    {
        var parameters = lambda.Params;
        var body = lambda.Body;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Body = body ?? throw new ArgumentNullException(nameof(body));
        // 使用轻量级闭包
        var freeVariables = ClosureAnalyzer.AnalyzeFreeVariables(lambda, closure);
        OptimizedClosure = LightweightClosure.CreateFromScope(closure, freeVariables);
        Closure = closure!; // 不保存完整作用域
        IsNative = false;
    }

    /// <summary>
    /// 创建原生函数（C# 代码实现）
    /// </summary>
    public FunctionValue(string name, Func<List<Value>, Value> nativeFunc)
    {
        Parameters = new List<string>();
        Body = null!;
        Closure = null!;
        NativeFunc = nativeFunc ?? throw new ArgumentNullException(nameof(nativeFunc));
        IsNative = true;
    }

    /// <summary>
    /// 创建原生函数（C# 代码实现）
    /// </summary>
    public FunctionValue(string name, Func<List<Value>, Task<Value>> nativeFunc)
    {
        Parameters = new List<string>();
        Body = null!;
        Closure = null!;
        NativeTask = nativeFunc ?? throw new ArgumentNullException(nameof(nativeFunc));
        IsNativeTask = true;
    }

    public override T As<T>()
    {
        if (this is T result) return result;
        throw new InvalidCastException($"Cannot cast FunctionValue to {typeof(T)}");
    }
    internal override Value Copy()
    {
        return this;
    }


    /// <summary>
    /// 调用函数（优化版本）
    /// </summary>
    public async Task<Value> CallAsync(ScriptEngine engine, params List<Value> args)
    {
        if (args.Count != ParameterCount)
        {
            throw new RuntimeException($"函数需要 {ParameterCount} 个参数, 但只传入了 {args.Count} 个参数");
        }
        if (IsNative)
        {
            return NativeFunc!(args);
        }
        else if (IsNativeTask)
        {
            return await NativeTask!(args);
        }
        // 根据闭包类型创建不同的作用域
        Scope callScope;
        if (OptimizedClosure != null)
        {
            // 使用轻量级闭包：只包含实际使用的变量
            callScope = new Scope(OptimizedClosure);
        }
        else
        {
            // 向后兼容：使用传统作用域链
            callScope = new Scope(Closure);
        }

        // 绑定参数（参数会遮蔽同名的闭包变量）
        for (int i = 0; i < Parameters.Count; i++)
        {
            callScope.Define(Parameters[i], args[i]);
        }

        // 执行函数体
        var result = await engine.EvaluateAsync(Body, callScope);
        return result;
    }

    /// <summary>
    /// 调用函数
    /// </summary>
    public async Task<Value> CallAsync2(ScriptEngine engine, params List<Value> args)
    {
        // 验证参数数量
        if (args.Count != ParameterCount)
        {
            throw new RuntimeException($"函数需要 {ParameterCount} 个参数, 但只传入了 {args.Count} 个参数");
        }

        if (IsNative)
        {
            return NativeFunc!(args);
        }
        else if (IsNativeTask)
        {
            return await NativeTask!(args);
        }


        // 创建新的作用域，闭包作为父作用域
        var callScope = new Scope(Closure);

        // 绑定参数
        for (int i = 0; i < Parameters.Count; i++)
        {
            callScope.Define(Parameters[i], args[i]);
        }

        // 执行函数体
        var result = await engine.EvaluateAsync(Body, callScope);
        return result;
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
    public object? ClrObject { get; } = Target; //?? throw new ArgumentNullException(nameof(Target));


    internal override Value Copy()
    {
        return new ClrObjectValue(Target);
    }


    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(ClrObjectValue)) return (T)(object)this;
        if (ClrObject is T typed) return typed;
        throw new InvalidCastException($"Cannot cast ClrObjectValue({ClrObject.GetType()}) to {typeof(T)}");
    }

    /// <summary>
    /// 获取对象的属性值（用于 ToString/Debug）
    /// </summary>
    public Dictionary<string, object> GetDebugProperties()
    {
        var result = new Dictionary<string, object>();
        var properties = ClrObject.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(ClrObject);
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
    internal override Value Copy()
    {
        return this;
    }

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
