namespace ScriptLang.Runtime;

/// <summary>
/// 日期时间值（不可变，UTC 存储）
/// </summary>
public class DateTimeValue(DateTime value) : Value
{
    /// <summary>
    /// 日期时间值（内部统一以 UTC 存储以保证比较和运算的一致性）
    /// </summary>
    public DateTime Value { get; } = value.Kind == DateTimeKind.Utc
        ? value
        : value.ToUniversalTime();

    public override bool IsDateTime => true;

    public override T As<T>()
    {
        if (this is T result) return result;
        if (typeof(T) == typeof(DateTimeValue)) return (T)(object)this;
        if (typeof(T) == typeof(DateTime)) return (T)(object)Value;
        if (typeof(T) == typeof(long)) return (T)(object)Value.Ticks;
        if (typeof(T) == typeof(string)) return (T)(object)ToString();
        throw new InvalidCastException($"无法将 DateTimeValue 转换为 {typeof(T)}");
    }

    /// <summary>
    /// 显示为本地时间
    /// </summary>
    public override string ToString() => Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");

    public override bool Equals(object? obj) =>
        obj is DateTimeValue other && Value.Equals(other.Value);

    public override int GetHashCode() => Value.GetHashCode();
}
