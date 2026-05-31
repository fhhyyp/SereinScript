using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

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
    /// 从轻量级闭包创建作用域（用于函数调用）
    /// </summary>
    public Scope(LightweightClosure closure)
    {
        Parent = null; // 轻量闭包已经包含了所有需要的父作用域变量

        // 加载闭包捕获的变量到当前作用域
        var capturedVars = closure.GetAllCapturedVariables();
        foreach (var (name, info) in capturedVars)
        {
            // 闭包变量在当前作用域中定义，但标记为已捕获
            _variables[name] = new VariableInfo(info.Value, info.IsMutable, IsCaptured: true);
        }
    }

    /// <summary>
    /// 定义变量
    /// </summary>
    public void Define(string name, Value value, bool isMutable = true)
    {
        if (_variables.ContainsKey(name))
        {
            throw new RuntimeException($"变量 '{name}' 已在此作用域中定义");
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
            throw new RuntimeException($"变量 '{name}' 已在此作用域中定义");
        }
        var value = new ClrObjectValue(data);
        _variables[name] = new VariableInfo(value, isMutable);
    }

    /*/// <summary>
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

        

        throw new RuntimeException($"未定义的变量 '{name}'");
    }*/

    /// <summary>
    /// 检查当前作用域是否存在某个变量
    /// </summary>
    /// <param name="varName"></param>
    /// <param name="info"></param>
    /// <returns></returns>
    internal bool TryGetValue(string varName, [NotNullWhen(true)] out VariableInfo? info)
    {
        if(_variables.TryGetValue(varName, out info))
        {
            return true;
        }
        if (Parent != null && Parent.TryGetValue(varName, out info))
        {
            return true;
        }
        return false;
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
                throw new RuntimeException($"无法为不可变变量 '{name}' 赋值");
            }
            _variables[name] = new VariableInfo(value, info.IsMutable);
            return;
        }
        
        if (Parent != null)
        {
            Parent.Set(name, value);
            return;
        }

        throw new RuntimeException($"未定义的变量 '{name}'");
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
public record VariableInfo(Value Value, bool IsMutable, bool IsCaptured = false);
