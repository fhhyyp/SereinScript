namespace ScriptLang.Runtime;

/// <summary>
/// 时间跨度值（不可变）
/// </summary>
public class TimeSpanValue(TimeSpan value) : Value
{
    /// <summary>
    /// 时间跨度值
    /// </summary>
    public TimeSpan Value { get; } = value;

    public override bool IsTimeSpan => true;

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(TimeSpanValue)) return (T)(object)this;
        if (typeof(T) == typeof(TimeSpan)) return (T)(object)Value;
        if (typeof(T) == typeof(long)) return (T)(object)Value.Ticks;
        if (typeof(T) == typeof(double)) return (T)(object)Value.TotalDays;
        if (typeof(T) == typeof(string)) return (T)(object)ToString();
        throw new InvalidCastException($"无法将 TimeSpanValue 转换为 {typeof(T)}");
    }

    public override string ToString() => Value.ToString();

    public override bool Equals(object? obj) =>
        obj is TimeSpanValue other && Value.Equals(other.Value);

    public override int GetHashCode() => Value.GetHashCode();
}
