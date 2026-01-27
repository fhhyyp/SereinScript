using System.Threading.Tasks;

namespace ScriptLang.Runtime;

/// <summary>
/// 函数值（Lambda + 闭包）
/// </summary>
public record FunctionValue : Value
{
    /// <summary>
    /// 参数名列表
    /// </summary>
    public List<string> Parameters { get; }
    
    /// <summary>
    /// 函数体（AST 表达式）
    /// </summary>
    public Parser.Expr Body { get; }
    
    /// <summary>
    /// 闭包作用域（捕获定义时的环境）
    /// </summary>
    public Scope Closure { get; }
    
    /// <summary>
    /// 是否是原生函数
    /// </summary>
    public bool IsNative { get; }
    public bool IsNativeTask { get; }
    
    /// <summary>
    /// 原生函数委托
    /// </summary>
    public Func<List<Value>, Value>? NativeFunc { get; }
    public Func<List<Value>, Task<Value>>? NativeTask { get; }
    
    public int ParameterCount => Parameters.Count;
    
    /// <summary>
    /// 创建 DSL Lambda 函数
    /// </summary>
    public FunctionValue(List<string> parameters, Parser.Expr body, Scope closure)
    {
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Closure = closure ?? throw new ArgumentNullException(nameof(closure));
        IsNative = false;
    }
    
    /// <summary>
    /// 创建原生函数（C# 代码实现）
    /// </summary>
    public FunctionValue(string name, Func<List<Value>, Value> nativeFunc)
    {
        Parameters = new List<string>();
        Body = null!;
        Closure = null!;
        NativeFunc = nativeFunc ?? throw new ArgumentNullException(nameof(nativeFunc));
        IsNative = true;
    }
    
    
    /// <summary>
    /// 创建原生函数（C# 代码实现）
    /// </summary>
    public FunctionValue(string name, Func<List<Value>, Task<Value>> nativeFunc)
    {
        Parameters = new List<string>();
        Body = null!;
        Closure = null!;
        NativeTask = nativeFunc ?? throw new ArgumentNullException(nameof(nativeFunc));
        IsNativeTask = true;
    }
    
    public override T As<T>()
    {
        if (this is T result) return result;
        throw new InvalidCastException($"Cannot cast FunctionValue to {typeof(T)}");
    }
    
    /// <summary>
    /// 调用函数
    /// </summary>
    public async Task<Value> CallAsync(List<Value> args, ScriptEngine engine)
    {
        if (IsNative)
        {
            return NativeFunc!(args);
        }
        else if (IsNativeTask)
        {
            return await NativeTask!(args);
        }

        // 验证参数数量
        if (args.Count != ParameterCount)
        {
            throw new RuntimeException(
                $"Function expects {ParameterCount} arguments, but got {args.Count}");
        }
        
        // 创建新的作用域，闭包作为父作用域
        var callScope = new Scope(Closure);

        BuiltinFunctions.RegisterAll(callScope);
        // 绑定参数
        for (int i = 0; i < Parameters.Count; i++)
        {
            callScope.Define(Parameters[i], args[i]);
        }
        
        // 执行函数体
        var result = await engine.EvaluateAsync(Body, callScope);
        return result;
    }
}
