namespace ScriptLang.Runtime.ByteCode;

/// <summary>   
/// 编译后的函数值
/// </summary>
/// <param name="Parameters">参数值</param>
/// <param name="Chunk">代码块</param>
/// <param name="Closure">闭包</param>
public record CompiledFunctionValue(List<string> Parameters, ByteCodeChunk Chunk, IClosureContext Closure) : Value, ICallable
{
    public override T As<T>()
    {
        if (this is T result) return result;
        throw new InvalidCastException($"Cannot cast CompiledFunctionValue to {typeof(T)}");
    }
    public async Task<Value> CallAsync(ScriptEngine engine, List<Value> args)
    {
        var vm = new VM(engine);
        var value = await vm.InvokeCompiledFunctionAsync(this, args);
        return value;
    }
}