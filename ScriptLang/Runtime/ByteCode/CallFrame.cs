using System.Collections.Concurrent;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>
/// 调用帧
/// </summary>
internal class CallFrame
{
    /// <summary>当前执行的字节码块</summary>
    public ByteCodeChunk Chunk  = null!;

    /// <summary>闭包上下文（变量作用域）</summary>
    public IClosureContext Closure   = null!;

    /// <summary>当前帧的指令指针</summary>
    public int IP = 0;

    /// <summary>返回地址（-1 表示顶层）</summary>
    // public int ReturnAddress { get; set; } = -1;

    /// <summary>局部变量</summary>
    public Dictionary<string, Value> Locals { get; } = new();

    /// <summary>当前作用域捕获的变量</summary>
    public Dictionary<string, VariableInfo> CapturedVariables { get; } = new();

    public void Init(ByteCodeChunk chunk, IClosureContext closure)
    {
        this.Chunk = chunk;
        this.Closure = closure;
        this.IP = 0;
        Locals.Clear();
        CapturedVariables.Clear();
    }
    public void Reset()
    {
        this.Chunk = null;
        this.Closure = null;
        this.IP = 0;
        Locals.Clear();
        CapturedVariables.Clear();
    }

}


internal sealed class CallFramePool
{
    private readonly ConcurrentStack<CallFrame> _pool = new();
    private readonly int _maxPoolSize;

    public CallFramePool(int maxPoolSize = 32)
    {
        _maxPoolSize = maxPoolSize;
    }

    public CallFrame Rent()
    {
        if (_pool.TryPop(out var frame))
            return frame;
        return new CallFrame();
    }

    public void Return(CallFrame frame)
    {
        if (_pool.Count < _maxPoolSize)
        {
            frame.Reset();
            _pool.Push(frame);
        }
        // 超出池大小限制时让 GC 回收
    }
}
