using ScriptLang.Parser;
using ScriptLang.Runtime.ByteCode;
using System.Data.SqlTypes;

namespace ScriptLang.Runtime
{

    /// <summary>
    /// 函数值（Lambda + 闭包）
    /// </summary>
    public record FunctionValue : Value, ICallable
    {
        /// <summary>
        /// 函数名称
        /// </summary>
        public string Name { get; private set; }

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
        public IClosureContext Closure { get; }


        /// <summary>
        /// 是否是原生函数
        /// </summary>
        public bool IsNative { get; }

        /// <summary>
        /// 是否为原生异步函数
        /// </summary>
        public bool IsNativeTask { get; }

        /// <summary>
        /// 原生函数委托
        /// </summary>
        public Func<List<Value>, Value>? NativeFunc { get; }

        /// <summary>
        /// 原生异步函数委托
        /// </summary>
        public Func<List<Value>, Task<Value>>? NativeTask { get; }

        /// <summary>
        /// 参数数量
        /// </summary>
        public int ParameterCount => Parameters.Count;

        /// <summary>
        /// 创建 DSL Lambda 函数（优化版本）
        /// </summary>
        public FunctionValue(LambdaExpr lambda, Scope closure)
        {
            Name = $"<lambda>({string.Join(",", lambda.Params)})=>{{}}";
            var parameters = lambda.Params;
            var body = lambda.Body;
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Body = body ?? throw new ArgumentNullException(nameof(body));

            // 分析捕获闭包变量
            var freeVariables = ClosureAnalyzer.AnalyzeFreeVariables(lambda, closure);
            Closure = LightweightClosure.CreateFromScope(closure, freeVariables);
            //Console.WriteLine("捕获:" + string.Join(",", freeVariables));
            IsNative = false;
        }

        /// <summary>
        /// 创建原生函数（C# 代码实现）
        /// </summary>
        public FunctionValue(string name, Func<List<Value>, Value> nativeFunc)
        {
            Name = name;
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
            Name = name;
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
        public async Task<Value> CallAsync(ScriptEngine engine, params List<Value> args)
        {
            if (IsNative)
            {
                return NativeFunc!(args);
            }
            else if (IsNativeTask)
            {
                return await NativeTask!(args);
            }

        
            if (args.Count != ParameterCount)
            {
                throw new RuntimeException($"函数需要 {ParameterCount} 个参数, 但只传入了 {args.Count} 个参数");
            }
            // 创建作用域
            Scope callScope = new Scope(Closure); //  Closure.CreateChildScope();

            // 绑定参数（参数会遮蔽同名的闭包变量）
            for (int i = 0; i < Parameters.Count; i++)
            {
                callScope.Define(Parameters[i], args[i]);
            }

            // 执行函数体
            var interpreter = new Interpreter(engine);
            var value = await interpreter.MainEvaluateAsync(Body, callScope);
            return value;
        }

    }
}
