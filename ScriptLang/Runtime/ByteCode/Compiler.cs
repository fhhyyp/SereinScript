using ScriptLang.Parser;

namespace ScriptLang.Runtime.ByteCode;
/// <summary>表示一条字节码指令</summary>
/// <param name="OpCode">指令类型</param>
/// <param name="Operand">操作数</param>
public sealed record Instruction(OpCode OpCode, object? Operand = null);


public sealed class Compiler
{
    private readonly List<Instruction> _code = [];
    private readonly ByteCodeChunk _chunk = new();

    // 编译时作用域：变量名 -> (索引, 是否可变, 是否被捕获)
    private readonly Stack<Dictionary<string, VariableBinding>> _scopeStack = new();

    // 当前作用域的捕获变量列表（用于闭包）
    private readonly List<string> _capturedVariables = [];

    //private readonly Stack<Dictionary<string, bool>> _scopeStack = new();

    // 跳转指令的待回填位置
    private readonly Stack<int> _patchStack = new();

    // 外部全局变量（编译时不可知，运行时查询）
    private readonly HashSet<string> _externalGlobals;

    // 是否允许未解析的变量（作为全局变量）
    public bool AllowUndeclaredVariables { get; set; } = true;

    public Compiler(HashSet<string>? knownGlobals = null)
    {
        _scopeStack.Push(new Dictionary<string, VariableBinding>());
        _externalGlobals = knownGlobals ?? new HashSet<string>();
    }

    /// <summary>编译表达式并返回字节码块</summary>
    public ByteCodeChunk Compile(Expr expr)
    {
        Visit(expr);

        // 确保最后有 Return 指令
        if (_code.Count == 0 || _code[^1].OpCode != OpCode.Return)
        {
            Emit(OpCode.Return);
        }

        _chunk.Code.AddRange(_code);
        return _chunk;
    }

    // ==================== 辅助方法 ====================

    private void Emit(OpCode op)
    {
        _code.Add(new Instruction(op));
    }

    private void Emit(OpCode op, object operand)
    {
        _code.Add(new Instruction(op, operand));
    }

    /// <summary>发射跳转指令并返回待回填的位置索引</summary>
    private int EmitJump(OpCode jumpOp)
    {
        Emit(jumpOp, -1); // 占位，稍后回填
        return _code.Count - 1;
    }

    /// <summary>回填跳转位置</summary>
    private void PatchJump(int jumpIndex)
    {
        int targetOffset = _code.Count;
        _code[jumpIndex] = new Instruction(
            _code[jumpIndex].OpCode,
            targetOffset
        );
    }

    /// <summary>进入新作用域</summary>
    private void PushScope()
    {
        _scopeStack.Push(new Dictionary<string, VariableBinding>());
    }

    /// <summary>退出当前作用域</summary>
    private void PopScope()
    {
        _scopeStack.Pop();
    }

    /// <summary>在当前作用域定义变量</summary>
    private void DefineVariable(string name, bool isMutable = false, bool isCaptured = false)
    {
        var currentScope = _scopeStack.Peek();
        if (currentScope.ContainsKey(name))
        {
            throw new InvalidOperationException($"变量 '{name}' 已在此作用域中定义");
        }
        currentScope[name] = new VariableBinding(0, isMutable, isCaptured);
    }

    /// <summary>查找变量（从内到外）</summary>
    private VariableBinding? ResolveVariable(string name)
    {
        // 1. 先查编译时作用域链
        foreach (var scope in _scopeStack)
        {
            if (scope.TryGetValue(name, out var binding))
            {
                // 在作用域链中找到的变量不是全局变量
                // 除非它明确标记为 IsGlobal（通过外部注入）
                return binding;
            }
        }

        // 2. 检查是否是已知的外部全局函数
        if (_externalGlobals.Contains(name))
        {
            // 明确的外部全局变量
            return new VariableBinding(-1, false, false) { IsGlobal = true };
        }

        // 3. 如果允许未声明变量，假定为全局变量
        if (AllowUndeclaredVariables)
        {
            // 未声明的变量当作全局变量
            return new VariableBinding(-1, true, false) { IsGlobal = true };
        }

        return null;
    }

    // ==================== 编译方法 ====================

    private void Visit(Expr expr)
    {
        switch (expr)
        {
            case ProgramExpr e:
                CompileProgram(e);
                break;
            case ErrorExpr e:
                CompileError(e);
                break;
            case LiteralExpr e:
                CompileLiteral(e);
                break;
            case IdentifierExpr e:
                CompileIdentifier(e);
                break;
            case LetExpr e:
                CompileLet(e);
                break;
            case VarExpr e:
                CompileVar(e);
                break;
            case AssignExpr e:
                CompileAssign(e);
                break;
            case IndexAssignExpr e:
                CompileIndexAssign(e);
                break;
            case BinaryExpr e:
                CompileBinary(e);
                break;
            case UnaryExpr e:
                CompileUnary(e);
                break;
            case ConditionalExpr e:
                CompileConditional(e);
                break;
            case ReturnExpr e:
                CompileReturn(e);
                break;
            case IfExpr e:
                CompileIf(e);
                break;
            case WhenExpr e:
                CompileWhen(e);
                break;
            case ForExpr e:
                CompileFor(e);
                break;
            case LambdaExpr e:
                CompileLambda(e);
                break;
            case CallExpr e:
                CompileCall(e);
                break;
            case BlockExpr e:
                CompileBlock(e);
                break;
            case ArrayLiteralExpr e:
                CompileArrayLiteral(e);
                break;
            case ObjectLiteralExpr e:
                CompileObjectLiteral(e);
                break;
            case MemberAccessExpr e:
                CompileMemberAccess(e);
                break;
            case MemberAssignExpr e:
                CompileMemberAssign(e);
                break;
            case IndexAccessExpr e:
                CompileIndexAccess(e);
                break;
            case ImportStmt e:
                CompileImport(e);
                break;
        }
    }

    /// <summary>编译程序（顶层表达式列表）</summary>
    private void CompileProgram(ProgramExpr expr)
    {
        foreach (var stmt in expr.Statements)
        {
            Visit(stmt);
            // 非表达式语句需要 Pop（它们的结果未被使用）
            if (IsStatement(stmt))
            {
                Emit(OpCode.Pop);
            }
        }
        Emit(OpCode.LoadNull);
        Emit(OpCode.Return);
    }

    /// <summary>判断是否为语句（返回值未被使用）</summary>
    private static bool IsStatement(Expr expr)
    {
        return expr is LetExpr or VarExpr or AssignExpr or ImportStmt;
    }

    /// <summary>编译解析异常表达式</summary>
    private void CompileError(ErrorExpr expr)
    {
        // 编译时错误，发射 LoadNull 占位
        Emit(OpCode.LoadNull);
    }

    /// <summary>编译字面量表达式</summary>
    private void CompileLiteral(LiteralExpr expr)
    {
        object? value = expr.Value;
        switch (value)
        {
            case null:
                Emit(OpCode.LoadNull);
                return;

            case true:
                Emit(OpCode.LoadTrue);
                return;

            case false:
                Emit(OpCode.LoadFalse);
                return;

            case -1:
                Emit(OpCode.LoadM1);
                return;

            case 0:
                Emit(OpCode.Load0);
                return;

            case 1:
                Emit(OpCode.Load1);
                return;
        }
        int constIndex = _chunk.AddConstant(value);
        Emit(OpCode.LoadConst, constIndex);
        
    }

    /// <summary>编译标识符引用表达式</summary>
    private void CompileIdentifier(IdentifierExpr expr)
    {
        string name = expr.Name;
        var binding = ResolveVariable(name);

        if (binding == null)
        {
            throw new InvalidOperationException($"未定义的变量 '{name}'");
        }

        // 导入成员默认使用 LoadVar 指令（编译时直接访问，不走全局查找）
        if (binding.IsImported)
        {
            Emit(OpCode.LoadVar, name);
            return;
        }

        // 全局变量使用 LoadGlobal 指令（运行时查找）
        if (binding.IsGlobal)
        {
            Emit(OpCode.LoadGlobal, name);
            return;
        }


        if (binding.IsCaptured)
        {
            int captureIndex = _capturedVariables.IndexOf(name);
            if (captureIndex >= 0)
            {
                Emit(OpCode.LoadCapture, captureIndex);
            }
            else
            {
                Emit(OpCode.LoadVar, name);
            }
        }
        else
        {
            Emit(OpCode.LoadVar, name);
        }
    }

    /// <summary>编译 Let 声明表达式</summary>
    private void CompileLet(LetExpr expr)
    {

        // 先计算初始值（此时新变量尚未定义，可以使用外部同名变量）
        Visit(expr.Value);

        // 在当前作用域定义变量（不可变）
        DefineVariable(expr.Name, isMutable: false);
        Emit(OpCode.StoreVar, expr.Name);
    }

    /// <summary>编译 Var 声明表达式</summary>
    private void CompileVar(VarExpr expr)
    {
        // 先计算初始值
        Visit(expr.Value);

        // 在当前作用域定义变量（可变）
        DefineVariable(expr.Name, isMutable: true);
        Emit(OpCode.StoreVar, expr.Name);
    }

    /// <summary>编译赋值表达式</summary>
    private void CompileAssign(AssignExpr expr)
    {
        Visit(expr.Value);

        var binding = ResolveVariable(expr.Name);
        if (binding?.IsGlobal == true)
        {
            Emit(OpCode.StoreGlobal, expr.Name);
        }
        else
        {
            Emit(OpCode.StoreVar, expr.Name);
        }
    }

    /// <summary>编译索引赋值表达式</summary>
    private void CompileIndexAssign(IndexAssignExpr expr)
    {
        // arr[index] = value
        Visit(expr.Target);   // 数组
        Visit(expr.Index);    // 索引
        Visit(expr.Value);    // 值
        Emit(OpCode.SetIndex);
    }

    /// <summary>编译二元运算表达式</summary>
    private void CompileBinary(BinaryExpr expr)
    {
        OpCode op = expr.Op switch
        {
            "+" => OpCode.Add,
            "-" => OpCode.Sub,
            "*" => OpCode.Mul,
            "/" => OpCode.Div,
            "%" => OpCode.Mod,
            "==" => OpCode.Equal,
            "!=" => OpCode.Ne,
            ">" => OpCode.Gt,
            ">=" => OpCode.Ge,
            "<" => OpCode.Lt,
            "<=" => OpCode.Le,
            "&&" => OpCode.And,
            "||" => OpCode.Or,
            _ => throw new InvalidOperationException($"未知的操作符 '{expr.Op}'")
        };

        // 短路求值：&& 和 ||
        if (expr.Op == "&&")
        {
            Visit(expr.Left);
            int jumpIndex = EmitJump(OpCode.JmpIfFalse);
            Emit(OpCode.Pop); // 弹出左边结果
            Visit(expr.Right);
            PatchJump(jumpIndex);
            return;
        }

        if (expr.Op == "||")
        {
            Visit(expr.Left);
            int jumpIndex = EmitJump(OpCode.JumpIfTrue);
            Emit(OpCode.Pop); // 弹出左边结果
            Visit(expr.Right);
            PatchJump(jumpIndex);
            return;
        }

        // 普通二元运算
        Visit(expr.Left);
        Visit(expr.Right);
        Emit(op);
    }

    /// <summary>编译一元运算表达式</summary>
    private void CompileUnary(UnaryExpr expr)
    {
        Visit(expr.Expr);

        OpCode op = expr.Op switch
        {
            "-" => OpCode.Neg,
            "!" => OpCode.Not,
            _ => throw new InvalidOperationException($"未知的一元操作符 '{expr.Op}'")
        };

        Emit(op);
    }

    /// <summary>编译三元条件表达式</summary>
    private void CompileConditional(ConditionalExpr expr)
    {
        Visit(expr.Cond);
        int elseJumpIndex = EmitJump(OpCode.JmpIfFalse);

        // Then 分支
        Emit(OpCode.Pop); // 弹出条件结果
        Visit(expr.Then);
        int endJumpIndex = EmitJump(OpCode.Jmp);

        // Else 分支
        PatchJump(elseJumpIndex);
        Emit(OpCode.Pop); // 弹出条件结果
        Visit(expr.Else);

        PatchJump(endJumpIndex);
    }

    /// <summary>编译 Return 表达式</summary>
    private void CompileReturn(ReturnExpr expr)
    {
        if (expr.Value != null)
        {
            Visit(expr.Value);
        }
        else
        {
            Emit(OpCode.LoadNull);
        }
        Emit(OpCode.Return);
    }

    /// <summary>编译 If-Then-Else 表达式</summary>
    private void CompileIf(IfExpr expr)
    {
        Visit(expr.Cond);
        int elseJumpIndex = EmitJump(OpCode.JmpIfFalse);

        // Then 分支
        Emit(OpCode.Pop);
        Visit(expr.Then);
        int endJumpIndex = EmitJump(OpCode.Jmp);

        // Else 分支
        PatchJump(elseJumpIndex);
        Emit(OpCode.Pop);
        Visit(expr.Else);

        PatchJump(endJumpIndex);
    }

    /// <summary>编译 When 表达式（模式匹配）</summary>
    private void CompileWhen(WhenExpr expr)
    {


        // 将 when 表达式转换为 if-else 链
        Visit(expr.Value);
        Emit(OpCode.StoreVar, "_when_temp");

        var jumpToEnd = new List<int>();

        foreach (var clause in expr.Clauses)
        {
            // 加载匹配值
            Emit(OpCode.LoadVar, "_when_temp");
            Visit(clause.Pattern);
            Emit(OpCode.Equal);

            int noMatchJump = EmitJump(OpCode.JmpIfFalse);

            // 匹配成功：执行 body
            Emit(OpCode.Pop);
            Visit(clause.Body);
            jumpToEnd.Add(EmitJump(OpCode.Jmp));

            PatchJump(noMatchJump);
            Emit(OpCode.Pop);
        }


        if (expr.OtherClause is not null)
        {
            var body = expr.OtherClause.Body;

            // 如果 Parser 错误地把 body 解析成了 Lambda，提取 Lambda 的 Body
            if (body is LambdaExpr lambda && lambda.Params.Count == 1 && lambda.Params[0] == "_")
            {
                Visit(lambda.Body);  // 编译 Lambda 内部的实际代码
            }
            else
            {
                Visit(body);
            }
        }
        else
        {
            // 没有 default 分支，返回 null
            Emit(OpCode.LoadNull);
        }

        foreach (int jump in jumpToEnd)
        {
            PatchJump(jump);
        }
    }

    /// <summary>编译 For 循环表达式</summary>
    private void CompileFor(ForExpr expr)
    {
        // 进入新的作用域
        PushScope();
        // 定义循环变量（可变）
        DefineVariable(expr.VarName, isMutable: true);

        // 解析迭代器
        Visit(expr.Iterable);
        Emit(OpCode.GetIterator);

        int loopStart = _code.Count;
        Emit(OpCode.MoveNext);
        int exitJump = EmitJump(OpCode.JmpIfFalse);

        Emit(OpCode.Current);
        Emit(OpCode.StoreVar, expr.VarName);

        // 循环体
        Visit(expr.Body);
        Emit(OpCode.Pop); // 丢弃循环体结果

        Emit(OpCode.Jmp, loopStart);
        PatchJump(exitJump);

        Emit(OpCode.LoadNull);

        // 退出作用域
        PopScope();
    }

    /// <summary>编译 Lambda 表达式</summary>
    private void CompileLambda(LambdaExpr expr)
    {
        // 分析自由变量
        var freeVariables = AnalyzeFreeVariables(expr);
#if DEBUG
        Console.WriteLine($"[Lambda] 参数: [{string.Join(", ", expr.Params)}]");
        Console.WriteLine($"[Lambda] 自由变量: [{string.Join(", ", freeVariables)}]");
#endif
        // 为每个捕获的变量发射 Capture 指令
        foreach (var varName in freeVariables)
        {
            var binding = ResolveVariable(varName);
            if (binding != null)
            {
                // 标记变量为已捕获
                binding.IsCaptured = true;
            }

            // 添加到捕获列表
            if (!_capturedVariables.Contains(varName))
            {
                _capturedVariables.Add(varName);
            }

            // 发射 Capture 指令
            Emit(OpCode.Capture, varName);
        }

        // 创建内部编译器
        var innerCompiler = new Compiler(_externalGlobals);
        innerCompiler.AllowUndeclaredVariables = this.AllowUndeclaredVariables;

        // 将当前 Lambda 的参数和捕获变量传递给内部编译器
        // 这样内部 Lambda 就知道这些变量存在
        var innerScope = innerCompiler._scopeStack.Peek();

        // 注册 Lambda 参数
        foreach (var param in expr.Params)
        {
            innerScope[param] = new VariableBinding(0, false, false);
        }

        // 注册捕获的变量
        foreach (var varName in freeVariables)
        {
            if (!innerScope.ContainsKey(varName))
            {
                innerScope[varName] = new VariableBinding(0, true, true)
                {
                    IsCaptured = true
                };
            }
        }

        // 编译内部函数体
        var closureChunk = innerCompiler.Compile(expr.Body);

        // 将闭包字节码块放入常量表
        int chunkIndex = _chunk.RegisterClosure(closureChunk);
        Emit(OpCode.CreateClosure, (chunkIndex, expr.Params, freeVariables));
    }

    /// <summary>分析 Lambda 中的自由变量</summary>
    private List<string> AnalyzeFreeVariables(LambdaExpr lambda)
    {
        var freeVars = new HashSet<string>();
        var boundVars = new HashSet<string>(lambda.Params);

        CollectFreeVariables(lambda.Body, boundVars, freeVars);
#if DEBUG
        Console.WriteLine($"[Closure] Lambda 参数: [{string.Join(", ", lambda.Params)}]");
        Console.WriteLine($"[Closure] 发现的自由变量: [{string.Join(", ", freeVars)}]");
#endif

        // 过滤：只保留真正需要捕获的外部变量
        var capturedVars = new List<string>();
        foreach (var varName in freeVars)
        {
            var binding = ResolveVariable(varName);

            if (binding != null)
            {
                // 只有真正的全局变量才跳过，Import 的变量也要捕获！
                // 注意：局部变量、导入变量、已捕获变量都需要捕获
                if (binding.IsGlobal && !binding.IsImported && !binding.IsCaptured)
                {
#if DEBUG
                    Console.WriteLine($"[Closure] 跳过全局变量: '{varName}'");
#endif
                }
                else
                {
#if DEBUG
                    Console.WriteLine($"[Closure] 捕获变量: '{varName}' (Imported={binding.IsImported}, Captured={binding.IsCaptured})");
#endif
                    capturedVars.Add(varName);
                }
                capturedVars.Add(varName);
            }
            else
            {
                // 变量在当前作用域链中不存在
                // 这可能是外层 Lambda 的参数（在嵌套闭包中）
#if DEBUG
                Console.WriteLine($"[Closure] 变量 '{varName}' 不在当前作用域链中");
#endif
                // 仍然需要捕获！它可能在外层闭包中
                capturedVars.Add(varName);
            }
        }
#if DEBUG
        Console.WriteLine($"[Closure] 最终捕获: [{string.Join(", ", capturedVars)}]");
#endif
        return capturedVars;
    }


    /// <summary>分析捕获变量</summary>
    /// <param name="expr"></param>
    /// <param name="bound"></param>
    /// <param name="free"></param>
    private void CollectFreeVariables(Expr expr, HashSet<string> bound, HashSet<string> free)
    {
        switch (expr)
        {
            case IdentifierExpr id:
                // 无论是否在 bound 中都要处理，只是不在 bound 中才加入 free
                if (!bound.Contains(id.Name))
                {
                    free.Add(id.Name);
                }
                break;

            case LiteralExpr:
                break;

            case LetExpr let:
                CollectFreeVariables(let.Value, bound, free);
                break;

            case VarExpr var:
                CollectFreeVariables(var.Value, bound, free);
                break;

            case AssignExpr assign:
                CollectFreeVariables(assign.Value, bound, free);
                // 赋值目标也是自由变量（如果不在 bound 中）
                if (!bound.Contains(assign.Name))
                {
                    free.Add(assign.Name);
                }
                break;

            case BlockExpr block:
                var blockBound = new HashSet<string>(bound);
                foreach (var stmt in block.Statements)
                {
                    if (stmt is LetExpr l)
                    {
                        CollectFreeVariables(l.Value, blockBound, free);
                        blockBound.Add(l.Name);
                    }
                    else if (stmt is VarExpr v)
                    {
                        CollectFreeVariables(v.Value, blockBound, free);
                        blockBound.Add(v.Name);
                    }
                    else
                    {
                        CollectFreeVariables(stmt, blockBound, free);
                    }
                }
                break;

            case LambdaExpr lambda:
                var lambdaBound = new HashSet<string>(bound);
                foreach (var param in lambda.Params)
                {
                    lambdaBound.Add(param);
                }
                CollectFreeVariables(lambda.Body, lambdaBound, free);
                break;

            case BinaryExpr binary:
                CollectFreeVariables(binary.Left, bound, free);
                CollectFreeVariables(binary.Right, bound, free);
                break;

            case UnaryExpr unary:
                CollectFreeVariables(unary.Expr, bound, free);
                break;

            case ConditionalExpr cond:
                CollectFreeVariables(cond.Cond, bound, free);
                CollectFreeVariables(cond.Then, bound, free);
                CollectFreeVariables(cond.Else, bound, free);
                break;

            case CallExpr call:
                CollectFreeVariables(call.Target, bound, free);
                foreach (var arg in call.Args)
                    CollectFreeVariables(arg, bound, free);
                break;

            case IfExpr ifExpr:
                CollectFreeVariables(ifExpr.Cond, bound, free);
                CollectFreeVariables(ifExpr.Then, bound, free);
                CollectFreeVariables(ifExpr.Else, bound, free);
                break;

            case WhenExpr whenExpr:
                CollectFreeVariables(whenExpr.Value, bound, free);
                foreach (var clause in whenExpr.Clauses)
                {
                    CollectFreeVariables(clause.Pattern, bound, free);
                    CollectFreeVariables(clause.Body, bound, free);
                }
                break;

            case ForExpr forExpr:
                CollectFreeVariables(forExpr.Iterable, bound, free);
                var forBound = new HashSet<string>(bound) { forExpr.VarName };
                CollectFreeVariables(forExpr.Body, forBound, free);
                break;

            case ReturnExpr ret:
                if (ret.Value != null)
                    CollectFreeVariables(ret.Value, bound, free);
                break;

            case ArrayLiteralExpr arr:
                foreach (var elem in arr.Elements)
                    CollectFreeVariables(elem, bound, free);
                break;

            case ObjectLiteralExpr obj:
                foreach (var prop in obj.Properties)
                {
                    CollectFreeVariables(prop.Value, bound, free);
                }
                break;

            case MemberAccessExpr member:
                CollectFreeVariables(member.Target, bound, free);
                break;

            case MemberAssignExpr memberAssign:
                CollectFreeVariables(memberAssign.Target, bound, free);
                CollectFreeVariables(memberAssign.Value, bound, free);
                break;

            case IndexAccessExpr index:
                CollectFreeVariables(index.Target, bound, free);
                CollectFreeVariables(index.Index, bound, free);
                break;

            case IndexAssignExpr indexAssign:
                CollectFreeVariables(indexAssign.Target, bound, free);
                CollectFreeVariables(indexAssign.Index, bound, free);
                CollectFreeVariables(indexAssign.Value, bound, free);
                break;

            case ProgramExpr program:
                var programBound = new HashSet<string>(bound);
                foreach (var stmt in program.Statements)
                {
                    if (stmt is LetExpr l)
                    {
                        CollectFreeVariables(l.Value, programBound, free);
                        programBound.Add(l.Name);
                    }
                    else if (stmt is VarExpr v)
                    {
                        CollectFreeVariables(v.Value, programBound, free);
                        programBound.Add(v.Name);
                    }
                    else
                    {
                        CollectFreeVariables(stmt, programBound, free);
                    }
                }
                break;

            case ErrorExpr:
            case ImportStmt:
                break;

            default:
#if DEBUG
                Console.WriteLine($"警告: CollectFreeVariables 未处理的表达式类型: {expr.GetType().Name}");
#endif
                break;
        }
    }
    /// <summary>编译函数调用表达式</summary>
    private void CompileCall(CallExpr expr)
    {
        // 先压入目标函数
        Visit(expr.Target);

        // 再压入参数
        foreach (var arg in expr.Args)
        {
            Visit(arg);
        }

        // 发射调用指令
        Emit(OpCode.Call, expr.Args.Count);
    }

    /// <summary>编译代码块表达式</summary>
    private void CompileBlock(BlockExpr expr)
    {
        PushScope();

        for (int i = 0; i < expr.Statements.Count; i++)
        {
            Visit(expr.Statements[i]);

            // 除最后一个表达式外，其余都 Pop
            if (i < expr.Statements.Count - 1)
            {
                Emit(OpCode.Pop);
            }
        }

        PopScope();
    }

    /// <summary>编译数组字面量表达式</summary>
    private void CompileArrayLiteral(ArrayLiteralExpr expr)
    {
        foreach (var element in expr.Elements)
        {
            Visit(element);
        }
        Emit(OpCode.CreateArray, expr.Elements.Count);
    }

    /// <summary>编译对象字面量表达式</summary>
    private void CompileObjectLiteral(ObjectLiteralExpr expr)
    {
        PushScope();
        DefineVariable("this", isMutable: true);  // 预声明 this
        // 压入属性名和属性值
        for (int i = expr.Properties.Count - 1; i >= 0; i--)
        {
            ObjectProperty? prop = expr.Properties[i];
            var propName = prop.Key;
            int propIndex = _chunk.AddConstant(propName);
            Emit(OpCode.LoadConst, propIndex);
            Visit(prop.Value);
        }
        Emit(OpCode.CreateObject, expr.Properties.Count);
        PopScope();
    }

    /// <summary>编译成员访问表达式</summary>
    private void CompileMemberAccess(MemberAccessExpr expr)
    {
        Visit(expr.Target);
        // 属性名称放入常量表
        int propIndex = _chunk.AddConstant(expr.Property);
        if (expr.SafeNull)
        {
            // ?. 安全访问：如果目标为 null，返回 null
            int nullJump = EmitJump(OpCode.JmpIfFalse);
            Emit(OpCode.Dup);
            Emit(OpCode.LoadConst, propIndex);
            Emit(OpCode.GetMember);
            int endJump = EmitJump(OpCode.Jmp);
            PatchJump(nullJump);
            Emit(OpCode.Pop);
            Emit(OpCode.LoadNull);
            PatchJump(endJump);
        }
        else
        {
            Emit(OpCode.LoadConst, propIndex);
            Emit(OpCode.GetMember);
        }
    }

    /// <summary>编译成员赋值表达式</summary>
    private void CompileMemberAssign(MemberAssignExpr expr)
    {
        Visit(expr.Target);
        int propIndex = _chunk.AddConstant(expr.Property);
        Emit(OpCode.LoadConst, propIndex);
        Visit(expr.Value);

        if (expr.SafeNull)
        {
            // ?. 安全赋值
            int nullJump = EmitJump(OpCode.JmpIfFalse);
            Emit(OpCode.SetMember);
            int endJump = EmitJump(OpCode.Jmp);
            PatchJump(nullJump);
            Emit(OpCode.Pop);
            Emit(OpCode.Pop);
            PatchJump(endJump);
        }
        else
        {
            Emit(OpCode.SetMember);
        }
    }

    /// <summary>编译索引访问表达式</summary>
    private void CompileIndexAccess(IndexAccessExpr expr)
    {
        Visit(expr.Target);
        Visit(expr.Index);
        Emit(OpCode.GetIndex);
    }

    /// <summary>编译 Import 语句</summary>
    private void CompileImport(ImportStmt expr)
    {
        // // Import 在编译时处理：将常量放入常量表，VM 执行时解析
        // int pathIndex = _chunk.AddConstant(expr.FilePath);
        // int membersIndex = _chunk.AddConstant(expr.Members);
        // Emit(OpCode.LoadConst, pathIndex);
        // Emit(OpCode.LoadConst, membersIndex);
        // // Import 需要特殊的 VM 支持，这里简化处理
        // Emit(OpCode.LoadNull); // 占位

        /* // 将路径放入常量表
         int pathIndex = _chunk.AddConstant(expr.FilePath);

         // 构建成员映射列表：(原始名称, 别名)
         var memberMappings = new List<(string member, string alias)>();
         foreach (var (member, alias) in expr.Members)
         {
             memberMappings.Add((member, alias ?? member));
         }
         int membersIndex = _chunk.AddConstant(memberMappings);
         // 发射 Import 指令：操作数是 (路径索引, 成员索引)
         Emit(OpCode.Import, (pathIndex, membersIndex));
         // 将导入的成员注册到编译时作用域
         var currentScope = _scopeStack.Peek();
         foreach (var (member, alias) in expr.Members)
         {
             var name = alias ?? member;
             if (!currentScope.ContainsKey(name))
             {
                 // 标记为"导入变量" - 运行时由 Import 指令赋值
                 currentScope[name] = new VariableBinding(0, isMutable: false, isCaptured: false)
                 {
                     IsImported = true  // 新增标记
                 };
             }
         }*/

        var importData = new List<object?> { expr.FilePath };

        foreach (var (member, alias) in expr.Members)
        {
            importData.Add(member);
            importData.Add(alias);  // null 表示没有别名
        }

        int dataIndex = _chunk.AddConstant(importData);
        Emit(OpCode.Import, dataIndex);

        // 注册到编译时作用域
        var currentScope = _scopeStack.Peek();
        foreach (var (member, alias) in expr.Members)
        {
            var name = alias ?? member;
            if (!currentScope.ContainsKey(name))
            {
                currentScope[name] = new VariableBinding(0, isMutable: false)
                {
                    IsImported = true
                };
            }
        }
    }
}

/// <summary>编译时的变量绑定信息</summary>
internal sealed class VariableBinding
{
    public int Slot { get; set; }
    public bool IsMutable { get; set; }
    public bool IsCaptured { get; set; }
    public bool IsGlobal { get; internal set; }
    public bool IsImported { get; internal set; }

    public VariableBinding(int slot, bool isMutable, bool isCaptured = false)
    {
        Slot = slot;
        IsMutable = isMutable;
        IsCaptured = isCaptured;
    }
}

