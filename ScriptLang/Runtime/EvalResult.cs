namespace ScriptLang.Runtime;


/// <summary>
/// 执行结果
/// </summary>
public class EvalResult(Value value, bool isReturn = false, bool hasValue = false)
{
    public Value Value { get; } = value;
    public bool IsReturn { get; } = isReturn;
    public bool HasValue { get; } = hasValue;

    /// <summary>
    /// 默认的空返回值
    /// </summary>
    public static readonly EvalResult Null = new EvalResult(Value.Null, isReturn: true, hasValue: false);

    /// <summary>
    /// 包装返回值
    /// </summary>
    /// <param name="value">需要包装的值</param>
    /// <returns></returns>
    public static EvalResult FormResult(Value value) => new(value, isReturn: false, hasValue: true);

}
