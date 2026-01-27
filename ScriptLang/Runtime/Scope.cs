namespace ScriptLang.Runtime;

/// <summary>
/// 作用域（支持嵌套和闭包）
/// </summary>
public class Scope
{
    private readonly Dictionary<string, VariableInfo> _variables = new();
    

    /// <summary>
    /// 父作用域
    /// </summary>
    public Scope? Parent { get; }
    
    /// <summary>
    /// 所有变量（只读）
    /// </summary>
    public IReadOnlyDictionary<string, VariableInfo> Variables => _variables;
    
    public Scope(Scope? parent = null)
    {
        Parent = parent;
    }

    /// <summary>
    /// 定义变量
    /// </summary>
    public void Define(string name, Value value, bool isMutable = true)
    {
        if (_variables.ContainsKey(name))
        {
            throw new RuntimeException($"Variable '{name}' already defined in this scope");
        }
        _variables[name] = new VariableInfo(value, isMutable);
    }
    
    /// <summary>
    /// 定义变量
    /// </summary>
    public void DefineClrObject(string name, object data, bool isMutable = true)
    {
        if (_variables.ContainsKey(name))
        {
            throw new RuntimeException($"Variable '{name}' already defined in this scope");
        }
        var value = new ClrObjectValue(data);
        _variables[name] = new VariableInfo(value, isMutable);
    }
    
    /// <summary>
    /// 获取变量
    /// </summary>
    public Value Get(string name)
    {
        if (_variables.TryGetValue(name, out var info))
        {
            return info.Value;
        }
        
        if (Parent != null)
        {
            return Parent.Get(name);
        }
        
        throw new RuntimeException($"Undefined variable '{name}'");
    }
    
    /// <summary>
    /// 设置变量（仅对可变变量）
    /// </summary>
    public void Set(string name, Value value)
    {
        if (_variables.TryGetValue(name, out var info))
        {
            if (!info.IsMutable)
            {
                throw new RuntimeException($"Cannot assign to immutable variable '{name}'");
            }
            _variables[name] = new VariableInfo(value, info.IsMutable);
            return;
        }
        
        if (Parent != null)
        {
            Parent.Set(name, value);
            return;
        }
        
        throw new RuntimeException($"Undefined variable '{name}'");
    }
    
    /// <summary>
    /// 检查变量是否存在（在当前或父作用域中）
    /// </summary>
    public bool Exists(string name)
    {
        if (_variables.ContainsKey(name)) return true;
        if (Parent != null) return Parent.Exists(name);
        return false;
    }
    
    /// <summary>
    /// 检查变量是否在当前作用域中定义
    /// </summary>
    public bool IsDefinedLocally(string name)
    {
        return _variables.ContainsKey(name);
    }
    
    /// <summary>
    /// 创建子作用域
    /// </summary>
    public Scope CreateChildScope()
    {
        return new Scope(this);
    }
}

/// <summary>
/// 变量信息
/// </summary>
public record VariableInfo(Value Value, bool IsMutable);
