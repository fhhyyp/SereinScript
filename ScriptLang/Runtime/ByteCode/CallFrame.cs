namespace ScriptLang.Runtime.ByteCode;

/// <summary>
/// 调用帧
/// </summary>
internal class CallFrame
{
    /// <summary>当前执行的字节码块</summary>
    public ByteCodeChunk Chunk { get; set; } = null!;

    /// <summary>闭包上下文（变量作用域）</summary>
    public IClosureContext Closure { get; set; } = null!;

    /// <summary>当前帧的指令指针</summary>
    public int IP { get; set; } = 0;

    /// <summary>返回地址（-1 表示顶层）</summary>
    // public int ReturnAddress { get; set; } = -1;

    /// <summary>局部变量</summary>
    public Dictionary<string, Value> Locals { get; } = new();

    /// <summary>捕获的变量</summary>
    public Dictionary<string, VariableInfo> CapturedVariables { get; } = new();
}
