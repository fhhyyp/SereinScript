namespace ScriptLang.Runtime;

/// <summary>
/// 扩展执行器返回结果的方法
/// </summary>
public static class EvalResultExtension
{
    public static EvalResult FormResult(this Value value)
    {
        return EvalResult.FormResult(value);
    }
    public static EvalResult Return(this Value value)
    {
        return EvalResult.Return(value);
    }
}

/// <summary>
/// 执行结果
/// </summary>
public readonly struct EvalResult
{
    public Value Value { get; }
    public bool IsReturn { get; }
    public bool HasValue { get; }

    public EvalResult(Value value, bool isReturn = false, bool hasValue = false)
    {
        Value = value;
        IsReturn = isReturn;
        HasValue = hasValue;
    }

    public static EvalResult FormResult(Value value) => new(value, false, true);
    public static EvalResult Return(Value value) => new(value, true, true);
    public static EvalResult ReturnNotValue() => new(Value.Null, true, false);
}
