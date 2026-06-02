namespace ScriptLang.Runtime.ByteCode;

/// <summary>表示一个字节码块</summary>
public sealed class ByteCodeChunk
{
    /// <summary>获取字节码指令列表</summary>
    public List<Instruction> Code { get; } = [];

    /// <summary>当前帧的指令指针</summary>
    public int IP { get; set; } = 0;
    /// <summary>常量表（LoadConst 使用）</summary>
    private readonly List<object?> _constants = [];

    /// <summary>常量去重索引</summary>
    private readonly Dictionary<object?, int> _constantMap = new();

    /// <summary>嵌套闭包代码块</summary>
    private readonly List<ByteCodeChunk> _closures = [];

    public int ConstantCount => _constants.Count;

    private const int NULL_INDEX = 0;
    private const int BOOL_TRUE_INDEX = 1;
    private const int BOOL_FALSE_INDEX = 2;

    private const int INT_MIN = -127;
    private const int INT_MAX = 128;

    private const int INT_OFFSET = 3;

    private const int CONSTANT_OFFSET =
        INT_OFFSET + (INT_MAX - INT_MIN + 1);

  

    /// <summary>
    /// 添加常量
    /// </summary>
    public int AddConstant(object? value)
    {
        switch (value)
        {
            case null:
                return NULL_INDEX;

            case bool b:
                return b
                    ? BOOL_TRUE_INDEX
                    : BOOL_FALSE_INDEX;

            case int i when i >= INT_MIN && i <= INT_MAX:
                return i - INT_MIN + INT_OFFSET;

            default:
                {
                    if (_constantMap.TryGetValue(value, out int existing))
                        return existing + CONSTANT_OFFSET;

                    int index = _constants.Count;

                    _constants.Add(value);
                    _constantMap.Add(value, index);

                    return index + CONSTANT_OFFSET;
                }
        }
    }

    /// <summary>
    /// 根据索引获取常量
    /// </summary>
    public object? GetConstant(int index)
    {
        if (index == NULL_INDEX)
            return null;

        if (index == BOOL_TRUE_INDEX)
            return true;

        if (index == BOOL_FALSE_INDEX)
            return false;

        if (index >= INT_OFFSET &&
            index < CONSTANT_OFFSET)
        {
            return index - INT_OFFSET + INT_MIN;
        }

        if (index >= CONSTANT_OFFSET)
        {
            return _constants[index - CONSTANT_OFFSET];
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    /// <summary>
    /// 添加闭包
    /// </summary>
    public int RegisterClosure(ByteCodeChunk closureChunk)
    {
        _closures.Add(closureChunk);
        return _closures.Count - 1;
    }

    /// <summary>
    /// 获取闭包
    /// </summary>
    public ByteCodeChunk GetClosure(int index)
    {
        if ((uint)index >= (uint)_closures.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _closures[index];
    }

    internal IEnumerable<object> GetConstants() => [.. this._constants];

}



/// <summary>表示一个字节码块</summary>
public sealed class ByteCodeChunk2
{
    /// <summary>获取字节码指令列表</summary>
    public List<Instruction> Code { get; } = [];

    /// <summary>当前帧的指令指针</summary>
    public int IP { get; set; } = 0;

    /// <summary>常量表（用于 LoadConst）</summary>
    private List<object?> Constants { get; } = [];

    /// <summary>嵌套闭包的代码块</summary>
    private List<ByteCodeChunk> Closures { get; } = [];
    public int ConstantCount => Constants.Count;

    /// <summary>添加闭包代码块</summary>
    public int AddClosure(ByteCodeChunk closureChunk)
    {
        Closures.Add(closureChunk);
        return Closures.Count - 1 ;
    }

    private const int BOOL_TRUE_INDEX = 0;
    private const int BOOL_FALSE_INDEX = 1;
    private const int INT_MIN = -127;
    private const int INT_MAX = 128;
    private const int INT_OFFSET = 2;  // 整数的起始索引
    private const int CONSTANT_OFFSET = 2 + (INT_MAX - INT_MIN + 1); // = 2 + 256 = 258


    /// <summary>添加常量并返回索引</summary>
    public int AddConstant(object? value)
    {
        if (value is bool @bool)
        {
            return @bool ? BOOL_TRUE_INDEX : BOOL_FALSE_INDEX;
        }
        if (value is int i && i >= INT_MIN && i <= INT_MAX)
        {
            return i + INT_OFFSET - INT_MIN;
            // -127 → -127 + 2 - (-127) = 2 
            // 0    → 0 + 2 - (-127) = 129 
            // 128  → 128 + 2 - (-127) = 257 
        }
        else
        {
            // 去重：相同值复用索引
            // 普通常量
            int index = Constants.IndexOf(value);
            if (index >= 0)
                return index + CONSTANT_OFFSET;

            Constants.Add(value);
            return Constants.Count - 1 + CONSTANT_OFFSET;
        }
        
    }

    /// <summary>
    /// 根据索引获取常量
    /// </summary>
    public object? GetConstant(int index)
    {
        return index switch
        {
            BOOL_TRUE_INDEX => true,
            BOOL_FALSE_INDEX => false,
            >= INT_OFFSET and < CONSTANT_OFFSET => index - INT_OFFSET + INT_MIN,
            >= CONSTANT_OFFSET => Constants[index - CONSTANT_OFFSET],
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }

    internal ByteCodeChunk GetClosure(int index)
    {
        if (index < 0 || index >= Closures.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return Closures[index];
    }

    internal IEnumerable<object> GetConstants() => this.Constants;

}

