using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using ScriptLang.Runtime.ByteCode;
using ScriptLang.Utils;

namespace ScriptLang.Runtime;



/// <summary>
/// 运行时值类型（统一类型系统）
/// </summary>
public abstract class Value
{
    public static readonly Value Null = NullValue.Default;

    public abstract T As<T>();

    #region 类型判断
    /// <summary> 是否为 null  </summary>
    public virtual bool IsNull => false;

    /// <summary> 是否为数值 </summary>
    public virtual bool IsNumber => false;
    public virtual bool IsNumber_Decimal => false;
    public virtual bool IsNumber_Double => false;
    public virtual bool IsNumber_Float => false;
    public virtual bool IsNumber_Long => false;
    public virtual bool IsNumber_Int => false;

    /// <summary>是否为可变数值</summary>
    public virtual bool IsMutableNumber => false;

    public virtual bool IsString => false;
    public virtual bool IsBool => false;
    public virtual bool IsObject => false;
    public virtual bool IsArray => false;
    public virtual bool IsFunction => false;
    public virtual bool IsClrObject => false;
    public virtual bool IsClrMethod => false;
    #endregion

    /// <summary>转换为不可变值（MutableNumber 重写）</summary>
    public virtual Value ToImmutableValue() => this;
    public string AsString() => (this as StringValue)?.Value ?? this.ToString();
    public virtual bool AsBool() => false;
    public virtual Dictionary<string, Value> AsObject() => [];
    public virtual List<Value> AsArray() => [];
    
    public override string ToString()
    {
        return this switch
        {
            ArrayValue a => "[" + string.Join(", ", a.Elements) + "]",
            BoolValue b => b.Value ? "true" : "false",
            ClrMethodValue cm => $"<clr:func>{cm.MethodInfo.DeclaringType?.Name}.{cm.MethodInfo.Name}()",
            ClrObjectValue co => $"<clr:obj>{co.Value?.GetType().FullName}",
            FunctionValue f => $"<func>({string.Join(',', f.Parameters)}) = {{}}",
            NullValue => "null",
            NumberValue<int> n => n.Value.ToString(CultureInfo.InvariantCulture),
            NumberValue<long> n => n.Value.ToString(CultureInfo.InvariantCulture),
            NumberValue<float> n => n.Value.ToString(CultureInfo.InvariantCulture),
            NumberValue<double> n => n.Value.ToString(CultureInfo.InvariantCulture),
            NumberValue<decimal> n => n.Value.ToString(CultureInfo.InvariantCulture),
            ObjectValue o => "{" + string.Join(", ", o.Properties.Select(kv => $"{kv.Key}: {kv.Value}")) + "}",
            StringValue s => s.Value,
            CompiledFunctionValue cf => $"<c:func>({string.Join(',', cf.Parameters)}) = {{}}",
            _ => "unknown"
        };
    }

}

/// <summary>
/// Null 值
/// </summary>
public class NullValue : Value
{
    public static NullValue Default => new NullValue();
    public override bool IsNull => true;

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(NullValue)) return (T)(object)this;
        throw new InvalidCastException($"类型 '{typeof(T)}' 无法转化为 'NullValue'");
    }
}

public static class NumberValueCache
{
    // 缓存常用整数（-128 到 127）
    public static readonly NumberValue<int>[] SmallIntegerCache;

    // 特殊值单例
    public static readonly NumberValue<double> NaN;
    public static readonly NumberValue<double> PositiveInfinity;
    public static readonly NumberValue<double> NegativeInfinity;

    public static readonly NumberValue<int> Int32_M1;
    public static readonly NumberValue<int> Int32_0;
    public static readonly NumberValue<int> Int32_1;

    static NumberValueCache()
    {
        NaN = NumberValueFactory.Create(double.NaN);
        PositiveInfinity = NumberValueFactory.Create(double.PositiveInfinity);
        NegativeInfinity = NumberValueFactory.Create(double.NegativeInfinity);

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
public class NumberValue<TNumber>(TNumber Value) : Value where TNumber : struct, IEquatable<TNumber>, IFormattable, IConvertible
{
    public TNumber Value { get; } = Value;

    public override bool IsNumber => IsNumber_Int || IsNumber_Double || IsNumber_Decimal || IsNumber_Long || IsNumber_Float;
    public override bool IsNumber_Decimal => Value is decimal;
    public override bool IsNumber_Double => Value is double;
    public override bool IsNumber_Float => Value is float;
    public override bool IsNumber_Long => Value is long;
    public override bool IsNumber_Int => Value is int;

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
/// 可变数值容器（用于 var 声明的局部变量，避免算术运算时反复创建 NumberValue 对象）
/// 原地修改，零堆分配
/// </summary>
public sealed class MutableNumber : Value
{
    internal enum NumberKind : byte
    {
        Int,
        Long,
        Float,
        Double,
        Decimal
    }

    internal NumberKind _kind;
    internal int _intValue;
    internal long _longValue;
    internal float _floatValue;
    internal double _doubleValue;
    internal decimal _decimalValue;

    private MutableNumber() { }

    /// <summary>转换为不可变值（MutableNumber 重写）</summary>
    public override bool IsMutableNumber => true;
    public override Value ToImmutableValue() => ToImmutable();

    public static MutableNumber Create(int value) =>
        new() { _kind = NumberKind.Int, _intValue = value };

    public static MutableNumber Create(long value) =>
        new() { _kind = NumberKind.Long, _longValue = value };

    public static MutableNumber Create(float value) =>
        new() { _kind = NumberKind.Float, _floatValue = value };

    public static MutableNumber Create(double value) =>
        new() { _kind = NumberKind.Double, _doubleValue = value };

    public static MutableNumber Create(decimal value) =>
        new() { _kind = NumberKind.Decimal, _decimalValue = value };

    public static MutableNumber FromNumberValue(Value value)
    {
        if (value.IsNumber_Decimal) return Create(value.As<decimal>());
        if (value.IsNumber_Double) return Create(value.As<double>());
        if (value.IsNumber_Float) return Create(value.As<float>());
        if (value.IsNumber_Long) return Create(value.As<long>());
        if (value.IsNumber_Int) return Create(value.As<int>());
        throw new InvalidCastException($"无法从 {value.GetType()} 创建 MutableNumber");
    }

    private void EnsureKind(NumberKind required)
    {
        if (_kind >= required) return;

        while (_kind < required)
        {
            switch (_kind)
            {
                case NumberKind.Int:
                    _longValue = _intValue;
                    _kind = NumberKind.Long;
                    break;
                case NumberKind.Long:
                    _floatValue = _longValue;
                    _kind = NumberKind.Float;
                    break;
                case NumberKind.Float:
                    _doubleValue = _floatValue;
                    _kind = NumberKind.Double;
                    break;
                case NumberKind.Double:
                    _decimalValue = (decimal)_doubleValue;
                    _kind = NumberKind.Decimal;
                    break;
            }
        }
    }

    private static NumberKind MaxKind(NumberKind a, NumberKind b) => a > b ? a : b;

    private NumberKind ResolveTargetKind(Value other)
    {
        if (other is MutableNumber mn)
            return MaxKind(_kind, mn._kind);
        if (other.IsNumber_Decimal) return MaxKind(_kind, NumberKind.Decimal);
        if (other.IsNumber_Double) return MaxKind(_kind, NumberKind.Double);
        if (other.IsNumber_Float) return MaxKind(_kind, NumberKind.Float);
        if (other.IsNumber_Long) return MaxKind(_kind, NumberKind.Long);
        return _kind;
    }

    public void AddInPlace(Value other)
    {
        var targetKind = ResolveTargetKind(other);
        EnsureKind(targetKind);

        switch (_kind)
        {
            case NumberKind.Int: _intValue += other.As<int>(); break;
            case NumberKind.Long: _longValue += other.As<long>(); break;
            case NumberKind.Float: _floatValue += other.As<float>(); break;
            case NumberKind.Double: _doubleValue += other.As<double>(); break;
            case NumberKind.Decimal: _decimalValue += other.As<decimal>(); break;
        }
    }

    public void SubInPlace(Value other)
    {
        var targetKind = ResolveTargetKind(other);
        EnsureKind(targetKind);

        switch (_kind)
        {
            case NumberKind.Int: _intValue -= other.As<int>(); break;
            case NumberKind.Long: _longValue -= other.As<long>(); break;
            case NumberKind.Float: _floatValue -= other.As<float>(); break;
            case NumberKind.Double: _doubleValue -= other.As<double>(); break;
            case NumberKind.Decimal: _decimalValue -= other.As<decimal>(); break;
        }
    }

    public void MulInPlace(Value other)
    {
        var targetKind = ResolveTargetKind(other);
        EnsureKind(targetKind);

        switch (_kind)
        {
            case NumberKind.Int: _intValue *= other.As<int>(); break;
            case NumberKind.Long: _longValue *= other.As<long>(); break;
            case NumberKind.Float: _floatValue *= other.As<float>(); break;
            case NumberKind.Double: _doubleValue *= other.As<double>(); break;
            case NumberKind.Decimal: _decimalValue *= other.As<decimal>(); break;
        }
    }

    public void DivInPlace(Value other)
    {
        var targetKind = ResolveTargetKind(other);
        if (targetKind < NumberKind.Double) targetKind = NumberKind.Double;
        EnsureKind(targetKind);

        switch (_kind)
        {
            case NumberKind.Double: _doubleValue /= other.As<double>(); break;
            case NumberKind.Decimal: _decimalValue /= other.As<decimal>(); break;
        }
    }

    public void ModInPlace(Value other)
    {
        var targetKind = ResolveTargetKind(other);
        EnsureKind(targetKind);

        switch (_kind)
        {
            case NumberKind.Int: _intValue %= other.As<int>(); break;
            case NumberKind.Long: _longValue %= other.As<long>(); break;
            case NumberKind.Float: _floatValue %= other.As<float>(); break;
            case NumberKind.Double: _doubleValue %= other.As<double>(); break;
            case NumberKind.Decimal: _decimalValue %= other.As<decimal>(); break;
        }
    }

    /// <summary>返回当前值的不可变副本（传参、返回时使用）</summary>
    public Value ToImmutable()
    {
        return _kind switch
        {
            NumberKind.Int => NumberValueFactory.Create(_intValue),
            NumberKind.Long => NumberValueFactory.Create(_longValue),
            NumberKind.Float => NumberValueFactory.Create(_floatValue),
            NumberKind.Double => NumberValueFactory.Create(_doubleValue),
            NumberKind.Decimal => NumberValueFactory.Create(_decimalValue),
            _ => Value.Null
        };
    }

    /// <summary>
    /// 直接用 int 值替换内部值（零分配，用于 RangeIterator）
    /// </summary>
    public void SetFromInt(int value)
    {
        _kind = NumberKind.Int;
        _intValue = value;
    }

    // <summary>
    /// 用另一个数值替换当前 MutableNumber 的内部值（不改变引用）
    /// </summary>
    public void SetFrom(Value value)
    {
        if (value.IsNumber_Decimal)
        {
            _kind = NumberKind.Decimal;
            _decimalValue = value.As<decimal>();
        }
        else if (value.IsNumber_Double)
        {
            _kind = NumberKind.Double;
            _doubleValue = value.As<double>();
        }
        else if (value.IsNumber_Float)
        {
            _kind = NumberKind.Float;
            _floatValue = value.As<float>();
        }
        else if (value.IsNumber_Long)
        {
            _kind = NumberKind.Long;
            _longValue = value.As<long>();
        }
        else if (value.IsNumber_Int)
        {
            _kind = NumberKind.Int;
            _intValue = value.As<int>();
        }
        else
        {
            throw new InvalidCastException($"无法从 {value.GetType()} 设置 MutableNumber 值");
        }
    }

    // ===== 类型检测 =====

    public override bool IsNumber => true;
    public override bool IsNumber_Int => _kind == NumberKind.Int;
    public override bool IsNumber_Long => _kind == NumberKind.Long;
    public override bool IsNumber_Float => _kind == NumberKind.Float;
    public override bool IsNumber_Double => _kind == NumberKind.Double;
    public override bool IsNumber_Decimal => _kind == NumberKind.Decimal;

    public override T As<T>()
    {
        if (this is T result) return result;

        return _kind switch
        {
            NumberKind.Int => (T)(object)_intValue,
            NumberKind.Long => (T)(object)_longValue,
            NumberKind.Float => (T)(object)_floatValue,
            NumberKind.Double => (T)(object)_doubleValue,
            NumberKind.Decimal => (T)(object)_decimalValue,
            _ => throw new InvalidCastException($"类型 '{typeof(T)}' 无法转化为 'MutableNumber'"),
        };
    }

    public override string ToString()
    {
        return _kind switch
        {
            NumberKind.Int => _intValue.ToString(),
            NumberKind.Long => _longValue.ToString(),
            NumberKind.Float => _floatValue.ToString(),
            NumberKind.Double => _doubleValue.ToString(),
            NumberKind.Decimal => _decimalValue.ToString(),
            _ => "0"
        };
    }
}


/// <summary>
/// 字符串值
/// </summary>
public class StringValue(string Value) : Value
{
    public string Value { get; } = Value;

    public override bool IsString => true;

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(StringValue)) return (T)(object)this;
        if (typeof(T) == typeof(string)) return (T)(object)Value;
        throw new InvalidCastException($"类型 '{typeof(T)}' 无法转化为 'StringValue'");
    }
}

/// <summary>
/// 布尔值
/// </summary>
public class BoolValue : Value
{
    public bool Value { get; private set; }

    public override bool IsBool => true;

    public override bool AsBool() => Value;

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(BoolValue)) return (T)(object)this;
        throw new InvalidCastException($"类型 '{typeof(T)}' 无法转化为 'BoolValue'");
    }

    public static readonly BoolValue True;
    public static readonly BoolValue False;
    private static readonly Lock @lock = new();
    static BoolValue()
    {
        lock (@lock)
        {
            True = new BoolValue(true);
            False = new BoolValue(false);
        }
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
public class ObjectValue(Dictionary<string, Value> Properties) : Value
{
    public Dictionary<string, Value> Properties { get; } = Properties;

    public override bool IsObject => true;
    public override Dictionary<string, Value> AsObject() => Properties;

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
        
        throw new InvalidCastException($"类型 '{typeof(T)}' 无法转化为 'ObjectValue'");
    }
}



/// <summary>
/// 数组值
/// </summary>
public class ArrayValue(List<Value> Elements) : Value //, IObservableValue
{
    public int Length => Elements.Count;

    public List<Value> Value => Elements;

    public List<Value> Elements { get; } = Elements;

    public override bool IsArray => true;

    public override List<Value> AsArray() => Elements;

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
        throw new InvalidCastException($"类型 '{typeof(T)}' 无法转化为 'ArrayValue'");
    }

}

/// <summary>
/// CLR 对象值（包装任意 C# 对象）
/// </summary>
public class ClrObjectValue(object? Target) : Value
{
    /// <summary>
    /// 获取包装的 C# 对象
    /// </summary>
    public object? Value { get; } = Target; //?? throw new ArgumentNullException(nameof(Target));

    public override bool IsClrObject => true;

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(ClrObjectValue)) return (T)(object)this;
        if (Value is T typed) return typed;
        throw new InvalidCastException($"类型 '{typeof(T)}' 无法转化为 'ClrObjectValue'");
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
public class ClrMethodValue(MethodInfo methodInfo, object? instance) : Value
{
    public override bool IsClrMethod => true;

    /// <summary>
    /// 包装的 MethodInfo
    /// </summary>
    public DelegateDetails Delegate { get; } = new DelegateDetails(methodInfo);

    /// <summary>
    /// 目标对象（实例方法需要，静态方法为 null）
    /// </summary>
    public object? TargetInstance { get; set; } = instance;

    /// <summary>
    /// 参数数量
    /// </summary>
    public int ParameterCount { get; } = methodInfo.GetParameters().Length;

    /// <summary>
    /// 是否是静态方法
    /// </summary>
    public bool IsStatic { get; } = methodInfo.IsStatic;

    /// <summary>
    /// 返回类型
    /// </summary>
    public System.Type ReturnType { get; } = methodInfo.ReturnType;
    public MethodInfo MethodInfo { get; } = methodInfo;

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(ClrMethodValue)) return (T)(object)this;

        throw new InvalidCastException($"类型 '{typeof(T)}' 无法转化为 'ClrMethodValue'");
    }

    internal async Task<object?> InvokeAsync(object?[]? args = null)
    {
        return await Delegate.InvokeAsync(TargetInstance, args);
    }
}


/// <summary>
/// 函数值（仅用于原生函数）
/// DSL Lambda 应通过 Compiler 编译为 CompiledFunctionValue
/// </summary>
public class FunctionValue : Value, ICallable
{
    public override bool IsFunction => true;

    /// <summary>函数名称</summary>
    public string Name { get; }

    /// <summary>参数名列表</summary>
    public List<string> Parameters { get; }

    /// <summary>是否是原生函数</summary>
    public bool IsNative { get; }

    /// <summary>是否为原生异步函数</summary>
    public bool IsNativeTask { get; }

    /// <summary>原生函数委托</summary>
    public Func<List<Value>, Value>? NativeFunc { get; }

    /// <summary>原生异步函数委托</summary>
    public Func<List<Value>, Task<Value>>? NativeTask { get; }

    /// <summary>参数数量</summary>
    public int ParameterCount => Parameters.Count;

    /// <summary>创建同步原生函数</summary>
    public FunctionValue(string name, Func<List<Value>, Value> nativeFunc)
    {
        Name = name;
        Parameters = [];
        NativeFunc = nativeFunc ?? throw new ArgumentNullException(nameof(nativeFunc));
        IsNative = true;
    }

    /// <summary>创建异步原生函数</summary>
    public FunctionValue(string name, Func<List<Value>, Task<Value>> nativeFunc)
    {
        Name = name;
        Parameters = [];
        NativeTask = nativeFunc ?? throw new ArgumentNullException(nameof(nativeFunc));
        IsNativeTask = true;
    }

    public override T As<T>()
    {
        if (this is T result) return result;

        throw new InvalidCastException($"类型 '{typeof(T)}' 无法转化为 'FunctionValue'");
    }

    /// <summary>调用函数</summary>
    public async Task<Value> CallAsync(ScriptEngine engine, params List<Value> args)
    {
        if (IsNative)
        {
            return NativeFunc!(args);
        }
        else if (IsNativeTask)
        {
            return await NativeTask!(args);
        }

        throw new RuntimeException($"FunctionValue 不支持 DSL Lambda 调用，请使用 CompiledFunctionValue");
    }
}

/// <summary>
/// 编译后的函数值
/// </summary>
public class CompiledFunctionValue(
    List<string> parameters,
    ByteCodeChunk chunk,
    VariableTable variableTable,
    LightweightClosure? closure) : Value, ICallable
{
    /// <summary>参数名列表</summary>
    public List<string> Parameters { get; } = parameters;

    /// <summary>字节码块</summary>
    public ByteCodeChunk Chunk { get; } = chunk;

    /// <summary>变量表（描述槽位布局）</summary>
    public VariableTable VariableTable { get; } = variableTable;

    /// <summary>轻量闭包（捕获的变量）</summary>
    public LightweightClosure? Closure { get; } = closure;

    public override T As<T>()
    {
        if (this is T result) return result;

        throw new InvalidCastException($"类型 '{typeof(T)}' 无法转化为 'CompiledFunctionValue'");
    }

    public async Task<Value> CallAsync(ScriptEngine engine, List<Value> args)
    {
        var vm = new VM(engine);
        var value = await vm.InvokeCompiledFunctionAsync(this, args);
        return value;
    }
}



/// <summary>
/// 惰性范围迭代器（不预创建数组，按需生成数值）
/// 用于 for i in range(start, end) 循环，避免创建海量临时对象
/// </summary>
public sealed class RangeIterator(int start, int end) : Value
{
    /// <summary>
    /// 获取当前 int 值（不创建 NumberValue 对象，零分配）
    /// </summary>
    public int CurrentInt() => _current;

    private readonly int _start = start;
    private readonly int _end = end;
    private int _current = start - 1;

    /// <summary>移动到下一个，返回是否还有元素</summary>
    public bool MoveNext()
    {
        _current++;
        return _current < _end;
    }

    /// <summary>获取当前值（返回缓存的 NumberValue，小整数走缓存）</summary>
    public Value Current()
    {
        return NumberValueFactory.Create(_current);
    }

    public override T As<T>()
    {
        if (this is T result) return result;
        throw new InvalidCastException($"Cannot cast RangeIterator to {typeof(T)}");
    }

    public override string ToString() => $"range({_start}, {_end})";
}