/*using ScriptLang.Runtime;

namespace ScriptLang;

/// <summary>
/// 测试 CLR 回调：接受 DSL Lambda 作为回调
/// </summary>
public class CallbackTest
{

    /// <summary>
    /// 模拟异步操作，完成后调用回调
    /// </summary>
    public async Task<Value> ProcessWithCallbackAsync(FunctionValue callback, Value data)
    {
        await Task.Delay(100); // 模拟异步操作
        var interpreter = new Interpreter();
        var result = await callback.CallAsync(new List<Value> { data }, interpreter);
        Console.WriteLine($"Callback result: {result}");
        return result;
    }

    /// <summary>
    /// 模拟过滤器，使用 DSL Lambda 过滤列表
    /// </summary>
    public async Task<List<int>> FilterAsync(FunctionValue predicate, List<int> items)
    {
        await Task.Delay(50);
        var result = new List<int>();
        var interpreter = new Interpreter();

        foreach (var item in items)
        {
            var predicateValue = await predicate.CallAsync(new List<Value> { NumberValue.Create(item) }, interpreter);
            if (predicateValue.IsBool && predicateValue.AsBool())
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// 模拟映射器，使用 DSL Lambda 转换列表
    /// </summary>
    public async Task<List<string>> MapAsync(FunctionValue mapper, List<int> items)
    {
        await Task.Delay(50);
        var result = new List<string>();
        var interpreter = new Interpreter();

        foreach (var item in items)
        {
            var mappedValue = await mapper.CallAsync(new List<Value> { NumberValue.Create(item) }, interpreter);
            result.Add(mappedValue.AsString());
        }

        return result;
    }

    /// <summary>
    /// 模拟归约器，使用 DSL Lambda 计算总和
    /// </summary>
    public async Task<int> ReduceAsync(FunctionValue reducer, List<int> items, int initial)
    {
        await Task.Delay(50);
        var accumulator = initial;
        var interpreter = new Interpreter();

        foreach (var item in items)
        {
            var result = await reducer.CallAsync(new List<Value> { NumberValue.Create(accumulator), NumberValue.Create(item) }, interpreter);
            if (result.IsNumber)
                accumulator = (int)result.AsNumber();
        }

        return accumulator;
    }

    /// <summary>
    /// 事件测试：多次触发回调
    /// </summary>
    public async Task EventTestAsync(FunctionValue callback, int count)
    {
        var interpreter = new Interpreter();
        for (int i = 0; i < count; i++)
        {
            await Task.Delay(50);
            await callback.CallAsync(new List<Value> { NumberValue.Create(i) }, interpreter);
        }
    }
}
*/