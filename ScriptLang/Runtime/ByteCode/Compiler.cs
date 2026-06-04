using ScriptLang.Parser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>表示一条字节码指令</summary>
/// <param name="OpCode">指令类型</param>
/// <param name="Operand">操作数</param>
public sealed record Instruction(OpCode OpCode, object? Operand = null);

/// <summary>编译时变量绑定信息</summary>
internal sealed class VariableBinding
{
    /// <summary>槽位索引（区域内的局部索引）</summary>
    public int Slot { get; set; }

    /// <summary>是否可变</summary>
    public bool IsMutable { get; set; }

    /// <summary>是否被闭包捕获</summary>
    public bool IsCaptured { get; set; }

    /// <summary>是否全局变量</summary>
    public bool IsGlobal { get; set; }

    /// <summary>是否导入变量</summary>
    public bool IsImported { get; set; }

    /// <summary>槽位区域</summary>
    public SlotRegion Region { get; set; } = SlotRegion.Local;

    public VariableBinding(int slot, bool isMutable, bool isCaptured = false)
    {
        Slot = slot;
        IsMutable = isMutable;
        IsCaptured = isCaptured;
    }
}

/// <summary>
/// AST → 字节码编译器（全槽位化版本）
/// 编译时分配槽位，Build 阶段统一修正运行时槽位索引
/// </summary>
public sealed class Compiler
{
    private readonly List<Instruction> _code = [];
    private readonly ByteCodeChunk _chunk = new();
    private readonly VariableTableBuilder _varTable = new();

    // 编译时作用域栈：变量名 → VariableBinding
    private readonly Stack<Dictionary<string, VariableBinding>> _scopeStack = new();

    // 当前 Lambda 的捕获变量名集合
    private HashSet<string>? _currentLambdaCaptures;

    // 已知的外部全局变量
    private readonly HashSet<string> _externalGlobals;

    // 局部变量名集合（用于闭包分析）
    private readonly HashSet<string> _localNames = new();

    // 待修正的槽位指令：(指令索引, 区域, 局部槽位)
    private readonly List<(int instructionIndex, SlotRegion region, int localSlot)> _pendingSlotFixups = new();

    // 跳转指令的待回填位置（保留兼容）
    private readonly Stack<int> _patchStack = new();

    public Compiler(HashSet<string>? knownGlobals = null)
    {
        _scopeStack.Push(new Dictionary<string, VariableBinding>());
        _externalGlobals = knownGlobals ?? new HashSet<string>();

        foreach (var name in _externalGlobals)
        {
            GlobalSlotRegistry.Register(name);
        }
    }

    /// <summary>编译表达式并返回字节码块</summary>
    public ByteCodeChunk Compile(Expr expr)
    {
        Visit(expr);

        if (_code.Count == 0 || _code[^1].OpCode != OpCode.Return)
        {
            Emit(OpCode.Return);
        }

        _chunk.Code.AddRange(_code);

        // 构建最终的 VariableTable
        var variableTable = _varTable.Build();
        _chunk.VariableTable = variableTable;

        // 修正所有槽位指令
        FixupSlots(variableTable);

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

    /// <summary>发射 LoadSlot 指令（占位，Build 时修正）</summary>
    private void EmitLoadSlot(VariableBinding binding)
    {
        int index = _code.Count;
        Emit(OpCode.LoadSlot, -1);
        _pendingSlotFixups.Add((index, binding.Region, binding.Slot));
    }

    /// <summary>发射 StoreSlot 指令（占位，Build 时修正）</summary>
    private void EmitStoreSlot(VariableBinding binding)
    {
        int index = _code.Count;
        Emit(OpCode.StoreSlot, -1);
        _pendingSlotFixups.Add((index, binding.Region, binding.Slot));
    }

    /// <summary>Build 后统一修正所有槽位指令</summary>
    private void FixupSlots(VariableTable vt)
    {
        foreach (var (index, region, localSlot) in _pendingSlotFixups)
        {
            int runtimeSlot = region switch
            {
                SlotRegion.Local => localSlot,
                SlotRegion.Capture => vt.CaptureOffset + localSlot,
                SlotRegion.Global => vt.GlobalOffset + localSlot,
                SlotRegion.Builtin => vt.BuiltinOffset + localSlot,
                _ => throw new InvalidOperationException($"未知的槽位区域: {region}")
            };

            _chunk.Code[index] = new Instruction(
                _chunk.Code[index].OpCode,
                runtimeSlot
            );
        }
    }

    private int EmitJump(OpCode jumpOp)
    {
        Emit(jumpOp, -1);
        return _code.Count - 1;
    }

    private void PatchJump(int jumpIndex)
    {
        int targetOffset = _code.Count;
        _code[jumpIndex] = new Instruction(
            _code[jumpIndex].OpCode,
            targetOffset
        );
    }

    private void PushScope()
    {
        _scopeStack.Push(new Dictionary<string, VariableBinding>());
    }

    private void PopScope()
    {
        _scopeStack.Pop();
    }

    private VariableBinding DefineVariable(string name, bool isMutable = false, bool isParameter = false)
    {
        var currentScope = _scopeStack.Peek();
        if (currentScope.ContainsKey(name))
        {
            throw new InvalidOperationException($"变量 '{name}' 已在此作用域中定义");
        }

        int slot = _varTable.AllocLocal(name, isParameter);
        _localNames.Add(name);

        var binding = new VariableBinding(slot, isMutable)
        {
            Region = SlotRegion.Local
        };
        currentScope[name] = binding;
        return binding;
    }

    private VariableBinding? ResolveVariable(string name)
    {
        // 1. 查编译时作用域链
        foreach (var scope in _scopeStack.Reverse())
        {
            if (scope.TryGetValue(name, out var binding))
            {
                // 检查是否在闭包内引用外层局部变量
                if (_currentLambdaCaptures != null &&
                    binding.Region == SlotRegion.Local &&
                    _localNames.Contains(name) &&
                    !_scopeStack.Peek().ContainsKey(name))
                {
                    _currentLambdaCaptures.Add(name);
                }
                return binding;
            }
        }

        // 2. 外部全局变量
        if (_externalGlobals.Contains(name))
        {
            int globalIndex = _varTable.AllocGlobal(name);
            return new VariableBinding(globalIndex, true)
            {
                IsGlobal = true,
                Region = SlotRegion.Global
            };
        }

        // 3. GlobalSlotRegistry
        if (GlobalSlotRegistry.IsRegistered(name))
        {
            int globalIndex = _varTable.AllocGlobal(name);
            return new VariableBinding(globalIndex, true)
            {
                IsGlobal = true,
                Region = SlotRegion.Global
            };
        }

        // 4. 内置函数
        if (BuiltinFunctions.FunctionCaches.Any(f => f.Name == name))
        {
            int builtinIndex = _varTable.AllocBuiltin(name);
            return new VariableBinding(builtinIndex, false)
            {
                Region = SlotRegion.Builtin
            };
        }

        return null;
    }

    // ==================== 分发 ====================

    private void Visit(Expr expr)
    {
        switch (expr)
        {
            case ProgramExpr e: CompileProgram(e); break;
            case ErrorExpr e: CompileError(e); break;
            case LiteralExpr e: CompileLiteral(e); break;
            case IdentifierExpr e: CompileIdentifier(e); break;
            case LetExpr e: CompileLet(e); break;
            case VarExpr e: CompileVar(e); break;
            case AssignExpr e: CompileAssign(e); break;
            case IndexAssignExpr e: CompileIndexAssign(e); break;
            case BinaryExpr e: CompileBinary(e); break;
            case UnaryExpr e: CompileUnary(e); break;
            case ConditionalExpr e: CompileConditional(e); break;
            case ReturnExpr e: CompileReturn(e); break;
            case IfExpr e: CompileIf(e); break;
            case WhenExpr e: CompileWhen(e); break;
            case ForExpr e: CompileFor(e); break;
            case LambdaExpr e: CompileLambda(e); break;
            case CallExpr e: CompileCall(e); break;
            case BlockExpr e: CompileBlock(e); break;
            case ArrayLiteralExpr e: CompileArrayLiteral(e); break;
            case ObjectLiteralExpr e: CompileObjectLiteral(e); break;
            case MemberAccessExpr e: CompileMemberAccess(e); break;
            case MemberAssignExpr e: CompileMemberAssign(e); break;
            case IndexAccessExpr e: CompileIndexAccess(e); break;
            case ImportStmt e: CompileImport(e); break;
        }
    }

    // ==================== 程序 ====================

    private void CompileProgram(ProgramExpr expr)
    {
        foreach (var stmt in expr.Statements)
        {
            Visit(stmt);
            if (IsStatement(stmt))
            {
                Emit(OpCode.Pop);
            }
        }
        Emit(OpCode.LoadNull);
        Emit(OpCode.Return);
    }

    private static bool IsStatement(Expr expr)
    {
        return expr is LetExpr or VarExpr or AssignExpr or ImportStmt;
    }

    // ==================== 错误 ====================

    private void CompileError(ErrorExpr expr)
    {
        Emit(OpCode.LoadNull);
    }

    // ==================== 字面量 ====================

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

    // ==================== 标识符 ====================

    private void CompileIdentifier(IdentifierExpr expr)
    {
        string name = expr.Name;
        var binding = ResolveVariable(name);

        if (binding == null)
        {
            throw new InvalidOperationException($"未定义的变量 '{name}'");
        }

        EmitLoadSlot(binding);
    }

    // ==================== 声明与赋值 ====================

    private void CompileLet(LetExpr expr)
    {
        Visit(expr.Value);

        // 在整个作用域链中查找
        VariableBinding? existingBinding = null;
        foreach (var scope in _scopeStack.Reverse())
        {
            if (scope.TryGetValue(expr.Name, out var binding))
            {
                existingBinding = binding;
                break;
            }
        }

        if (existingBinding != null)
        {
            EmitStoreSlot(existingBinding);
        }
        else
        {
            var binding = DefineVariable(expr.Name, isMutable: false);
            EmitStoreSlot(binding);
        }
    }

    private void CompileVar(VarExpr expr)
    {
        Visit(expr.Value);

        VariableBinding? existingBinding = null;
        foreach (var scope in _scopeStack.Reverse())
        {
            if (scope.TryGetValue(expr.Name, out var binding))
            {
                existingBinding = binding;
                break;
            }
        }

        if (existingBinding != null)
        {
            EmitStoreSlot(existingBinding);
        }
        else
        {
            var binding = DefineVariable(expr.Name, isMutable: true);
            EmitStoreSlot(binding);
        }
    }

    private void CompileAssign(AssignExpr expr)
    {
        Visit(expr.Value);
        var binding = ResolveVariable(expr.Name); 
        if (binding == null)
        {
            throw new InvalidOperationException($"未定义的变量 '{expr.Name}'");
        }
        EmitStoreSlot(binding);
    }

    private void CompileIndexAssign(IndexAssignExpr expr)
    {
        Visit(expr.Target);
        Visit(expr.Index);
        Visit(expr.Value);
        Emit(OpCode.SetIndex);
    }

    // ==================== 二元运算 ====================

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

        if (expr.Op == "&&")
        {
            Visit(expr.Left);
            int jumpIndex = EmitJump(OpCode.JmpIfFalse);
            Emit(OpCode.Pop);
            Visit(expr.Right);
            PatchJump(jumpIndex);
            return;
        }

        if (expr.Op == "||")
        {
            Visit(expr.Left);
            int jumpIndex = EmitJump(OpCode.JumpIfTrue);
            Emit(OpCode.Pop);
            Visit(expr.Right);
            PatchJump(jumpIndex);
            return;
        }

        Visit(expr.Left);
        Visit(expr.Right);
        Emit(op);
    }

    // ==================== 一元运算 ====================

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

    // ==================== 三元条件 ====================

    private void CompileConditional(ConditionalExpr expr)
    {
        Visit(expr.Cond);
        int elseJumpIndex = EmitJump(OpCode.JmpIfFalse);

        Emit(OpCode.Pop);
        Visit(expr.Then);
        int endJumpIndex = EmitJump(OpCode.Jmp);

        PatchJump(elseJumpIndex);
        Emit(OpCode.Pop);
        Visit(expr.Else);

        PatchJump(endJumpIndex);
    }

    // ==================== 控制流 ====================

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

    private void CompileIf(IfExpr expr)
    {
        Visit(expr.Cond);
        int elseJumpIndex = EmitJump(OpCode.JmpIfFalse);

        Emit(OpCode.Pop);
        Visit(expr.Then);
        int endJumpIndex = EmitJump(OpCode.Jmp);

        PatchJump(elseJumpIndex);
        Emit(OpCode.Pop);
        Visit(expr.Else);

        PatchJump(endJumpIndex);
    }

    private void CompileWhen(WhenExpr expr)
    {
        var tempBinding = DefineVariable("_when_temp", isMutable: true);

        Visit(expr.Value);
        EmitStoreSlot(tempBinding);

        var jumpToEnd = new List<int>();

        foreach (var clause in expr.Clauses)
        {
            EmitLoadSlot(tempBinding);
            Visit(clause.Pattern);
            Emit(OpCode.Equal);

            int noMatchJump = EmitJump(OpCode.JmpIfFalse);

            Emit(OpCode.Pop);
            Visit(clause.Body);
            jumpToEnd.Add(EmitJump(OpCode.Jmp));

            PatchJump(noMatchJump);
            Emit(OpCode.Pop);
        }

        if (expr.OtherClause is not null)
        {
            var body = expr.OtherClause.Body;
            if (body is LambdaExpr lambda && lambda.Params.Count == 1 && lambda.Params[0] == "_")
            {
                Visit(lambda.Body);
            }
            else
            {
                Visit(body);
            }
        }
        else
        {
            Emit(OpCode.LoadNull);
        }

        foreach (int jump in jumpToEnd)
        {
            PatchJump(jump);
        }
    }

    // ==================== For 循环 ====================

    private void CompileFor(ForExpr expr)
    {
        PushScope();
        var loopBinding = DefineVariable(expr.VarName, isMutable: true);

        Visit(expr.Iterable);
        Emit(OpCode.GetIterator);

        int loopStart = _code.Count;
        Emit(OpCode.MoveNext);
        int exitJump = EmitJump(OpCode.JmpIfFalse);

        Emit(OpCode.Current);
        EmitStoreSlot(loopBinding);

        Visit(expr.Body);
        Emit(OpCode.Pop);

        Emit(OpCode.Jmp, loopStart);
        PatchJump(exitJump);

        Emit(OpCode.LoadNull);
        PopScope();
    }

    // ==================== Lambda 与闭包 ====================
    /// <summary>
    /// 编译 Lambda 表达式
    /// 
    /// 闭包捕获流程：
    /// 1. 分析当前 Lambda 直接引用的自由变量
    /// 2. 第一遍编译：用内部编译器编译 Body，收集嵌套闭包的捕获需求
    /// 3. 如果嵌套闭包需要捕获外层局部变量，第二遍编译：将这些变量预注册为捕获变量
    /// 4. 分配捕获槽位，发射 CreateClosure
    /// </summary>
    private void CompileLambda(LambdaExpr expr)
    {
#if DEBUG
        Console.WriteLine($"[Compiler.CompileLambda] === 开始编译 Lambda ===");
        Console.WriteLine($"[Compiler.CompileLambda] 参数: [{string.Join(", ", expr.Params)}]");
        Console.WriteLine($"[Compiler.CompileLambda] _localNames 内容 ({_localNames.Count} 个): [{string.Join(", ", _localNames)}]");
#endif

        // ===== 第 1 步：分析当前 Lambda 直接引用的自由变量 =====
        _currentLambdaCaptures = new HashSet<string>();
        var freeVars = CaptureAnalysis.Analyze(expr, _localNames);
        _currentLambdaCaptures = null;

#if DEBUG
        Console.WriteLine($"[Compiler.CompileLambda] 直接自由变量 ({freeVars.Count} 个): [{string.Join(", ", freeVars)}]");
#endif

        // ===== 第 2 步：第一遍编译，收集嵌套闭包捕获需求 =====
        var innerCompiler1 = CreateInnerCompiler(expr, freeVars, null);
        var closureChunk = innerCompiler1.Compile(expr.Body);

        // 收集所有嵌套闭包需要的捕获变量名
        var allCaptureNames = new HashSet<string>(freeVars);
        CollectNestedCaptures(closureChunk, allCaptureNames);

        var nestedOnlyVars = new HashSet<string>(allCaptureNames);
        nestedOnlyVars.ExceptWith(freeVars);

#if DEBUG
        Console.WriteLine($"[Compiler.CompileLambda] 直接自由变量: [{string.Join(", ", freeVars)}]");
        Console.WriteLine($"[Compiler.CompileLambda] 仅嵌套闭包引用的变量: [{string.Join(", ", nestedOnlyVars)}]");
        Console.WriteLine($"[Compiler.CompileLambda] 所有需要捕获的变量名 ({allCaptureNames.Count} 个): [{string.Join(", ", allCaptureNames)}]");
#endif

        // ===== 第 3 步：如果有嵌套捕获变量，第二遍重新编译 =====
        if (nestedOnlyVars.Count > 0)
        {
#if DEBUG
            Console.WriteLine($"[Compiler.CompileLambda] 检测到嵌套捕获变量，执行第二遍编译...");
#endif
            var innerCompiler2 = CreateInnerCompiler(expr, freeVars, nestedOnlyVars);
            closureChunk = innerCompiler2.Compile(expr.Body);

            // 重新收集（验证用）
            var verifyCaptureNames = new HashSet<string>(freeVars);
            CollectNestedCaptures(closureChunk, verifyCaptureNames);
#if DEBUG
            Console.WriteLine($"[Compiler.CompileLambda] 第二遍编译后嵌套捕获: [{string.Join(", ", verifyCaptureNames.Except(freeVars))}]");
#endif
        }

        // ===== 第 4 步：在当前帧为所有需要捕获的变量分配捕获槽位 =====
        var captureSlots = new Dictionary<string, int>();

        foreach (var varName in allCaptureNames)
        {
            if (nestedOnlyVars.Contains(varName))
            {
                // 仅被嵌套闭包引用的变量：直接强制分配捕获槽位
                int captureIndex = _varTable.AllocCapture(varName);
                captureSlots[varName] = captureIndex;
#if DEBUG
                Console.WriteLine($"[Compiler.CompileLambda] 强制分配捕获槽位（嵌套闭包变量）: '{varName}' → captureSlot={captureIndex}");
#endif
            }
            else
            {
                // 当前 Lambda 直接引用的自由变量：通过 ResolveVariable 确认类型
                var binding = ResolveVariable(varName);
                if (binding != null && (binding.Region == SlotRegion.Local || binding.Region == SlotRegion.Capture))
                {
                    int captureIndex = _varTable.AllocCapture(varName);
                    captureSlots[varName] = captureIndex;
                    binding.IsCaptured = true;
#if DEBUG
                    Console.WriteLine($"[Compiler.CompileLambda] 分配捕获槽位: '{varName}' → captureSlot={captureIndex} (原 Region={binding.Region})");
#endif
                }
#if DEBUG
                else if (binding != null)
                {
                    Console.WriteLine($"[Compiler.CompileLambda] 跳过: '{varName}' (Region={binding.Region})");
                }
#endif
            }
        }

        // ===== 第 5 步：构建 captureMappings 并发射 CreateClosure =====
        var captureMappings = captureSlots
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

#if DEBUG
        Console.WriteLine($"[Compiler.CompileLambda] captureMappings: [{string.Join(", ", captureMappings.Select(m => $"('{m.Key}', {m.Value})"))}]");
#endif

        int chunkIndex = _chunk.RegisterClosure(closureChunk);
        Emit(OpCode.CreateClosure, (chunkIndex, expr.Params, captureMappings));
    }

    /// <summary>
    /// 创建内部编译器，注册参数和捕获变量
    /// </summary>
    /// <param name="expr">Lambda 表达式</param>
    /// <param name="directFreeVars">当前 Lambda 直接引用的自由变量</param>
    /// <param name="nestedCaptureVars">嵌套闭包需要捕获的变量（null 表示第一遍编译）</param>
    private Compiler CreateInnerCompiler(LambdaExpr expr, HashSet<string> directFreeVars, HashSet<string>? nestedCaptureVars)
    {
        var innerCompiler = new Compiler(_externalGlobals);
        var innerScope = innerCompiler._scopeStack.Peek();

        // 注册 Lambda 参数
        foreach (var param in expr.Params)
        {
            int paramSlot = innerCompiler._varTable.AllocLocal(param, isParameter: true);
            innerCompiler._localNames.Add(param);
            innerScope[param] = new VariableBinding(paramSlot, false)
            {
                Region = SlotRegion.Local
            };
        }

        // 注册直接捕获的变量
        foreach (var varName in directFreeVars)
        {
            int captureIndex = innerCompiler._varTable.AllocCapture(varName);
            innerCompiler._localNames.Add(varName);
            innerScope[varName] = new VariableBinding(captureIndex, true, isCaptured: true)
            {
                Region = SlotRegion.Capture
            };
        }

        // 注册嵌套闭包需要的捕获变量（第二遍编译时）
        if (nestedCaptureVars != null)
        {
            foreach (var varName in nestedCaptureVars)
            {
                if (innerScope.ContainsKey(varName))
                    continue;

                int captureIndex = innerCompiler._varTable.AllocCapture(varName);
                innerCompiler._localNames.Add(varName);
                innerScope[varName] = new VariableBinding(captureIndex, true, isCaptured: true)
                {
                    Region = SlotRegion.Capture
                };
#if DEBUG
                Console.WriteLine($"[CreateInnerCompiler] 预注册嵌套捕获变量: '{varName}' → 捕获槽位 {captureIndex}");
#endif
            }
        }

        return innerCompiler;
    }

    /// <summary>
    /// 递归收集嵌套闭包中所有 CaptureNames 中的变量名
    /// </summary>
    private static void CollectNestedCaptures(ByteCodeChunk chunk, HashSet<string> captureNames)
    {
        var vt = chunk.VariableTable;
        if (vt != null && vt.CaptureCount > 0)
        {
            foreach (var captureName in vt.CaptureNames.Keys)
            {
#if DEBUG
                if (!captureNames.Contains(captureName))
                    Console.WriteLine($"[CollectNestedCaptures] 从嵌套闭包中发现: '{captureName}'");
#endif
                captureNames.Add(captureName);
            }
        }

        foreach (var nestedChunk in chunk.GetClosures())
        {
            CollectNestedCaptures(nestedChunk, captureNames);
        }
    }
    /*    /// <summary>
        /// 编译 Lambda 表达式
        /// </summary>
        private void CompileLambda(LambdaExpr expr)
        {
    #if DEBUG
            Console.WriteLine($"[Compiler.CompileLambda] === 开始编译 Lambda ===");
            Console.WriteLine($"[Compiler.CompileLambda] 参数: [{string.Join(", ", expr.Params)}]");
            Console.WriteLine($"[Compiler.CompileLambda] _localNames 内容 ({_localNames.Count} 个): [{string.Join(", ", _localNames)}]");
    #endif

            // ===== 第 1 步：分析当前 Lambda 直接引用的自由变量 =====
            _currentLambdaCaptures = new HashSet<string>();
            var freeVars = CaptureAnalysis.Analyze(expr, _localNames);
            _currentLambdaCaptures = null;

    #if DEBUG
            Console.WriteLine($"[Compiler.CompileLambda] 直接自由变量 ({freeVars.Count} 个): [{string.Join(", ", freeVars)}]");
    #endif

            // ===== 第 2 步：创建内部编译器 =====
            var innerCompiler = new Compiler(_externalGlobals);
            var innerScope = innerCompiler._scopeStack.Peek();

            // 注册 Lambda 参数为内部局部变量
            foreach (var param in expr.Params)
            {
                int paramSlot = innerCompiler._varTable.AllocLocal(param, isParameter: true);
                innerCompiler._localNames.Add(param);
                innerScope[param] = new VariableBinding(paramSlot, false)
                {
                    Region = SlotRegion.Local
                };
            }

            // 注册当前 Lambda 直接捕获的变量到内部编译器
            foreach (var varName in freeVars)
            {
                int captureIndex = innerCompiler._varTable.AllocCapture(varName);
                innerCompiler._localNames.Add(varName);
                innerScope[varName] = new VariableBinding(captureIndex, true, isCaptured: true)
                {
                    Region = SlotRegion.Capture
                };
            }

            // ===== 第 3 步：编译内部函数体 =====
            var closureChunk = innerCompiler.Compile(expr.Body);

            // ===== 第 4 步：收集所有嵌套闭包需要的捕获变量名 =====
            var allCaptureNames = new HashSet<string>(freeVars);
            CollectNestedCaptures(closureChunk, allCaptureNames);

            // 区分两种来源：直接引用 vs 仅嵌套闭包引用
            var nestedOnlyVars = new HashSet<string>(allCaptureNames);
            nestedOnlyVars.ExceptWith(freeVars);

    #if DEBUG
            Console.WriteLine($"[Compiler.CompileLambda] 直接自由变量: [{string.Join(", ", freeVars)}]");
            Console.WriteLine($"[Compiler.CompileLambda] 仅嵌套闭包引用的变量: [{string.Join(", ", nestedOnlyVars)}]");
            Console.WriteLine($"[Compiler.CompileLambda] 所有需要捕获的变量名 ({allCaptureNames.Count} 个): [{string.Join(", ", allCaptureNames)}]");
    #endif

            // ===== 第 5 步：在当前帧为所有需要捕获的变量分配捕获槽位 =====
            var captureSlots = new Dictionary<string, int>();

            foreach (var varName in allCaptureNames)
            {
                if (nestedOnlyVars.Contains(varName))
                {
                    // 仅被嵌套闭包引用的变量：直接强制分配捕获槽位
                    // 不经过 ResolveVariable，避免被同名全局/内置函数干扰
                    int captureIndex = _varTable.AllocCapture(varName);
                    captureSlots[varName] = captureIndex;

    #if DEBUG
                    Console.WriteLine($"[Compiler.CompileLambda] 强制分配捕获槽位（嵌套闭包变量）: '{varName}' → captureSlot={captureIndex}");
    #endif
                }
                else
                {
                    // 当前 Lambda 直接引用的自由变量：通过 ResolveVariable 确认类型
                    var binding = ResolveVariable(varName);

                    if (binding != null && (binding.Region == SlotRegion.Local || binding.Region == SlotRegion.Capture))
                    {
                        int captureIndex = _varTable.AllocCapture(varName);
                        captureSlots[varName] = captureIndex;
                        binding.IsCaptured = true;

    #if DEBUG
                        Console.WriteLine($"[Compiler.CompileLambda] 分配捕获槽位: '{varName}' → captureSlot={captureIndex} (原 Region={binding.Region})");
    #endif
                    }
                    else if (binding != null && (binding.Region == SlotRegion.Global || binding.Region == SlotRegion.Builtin))
                    {
    #if DEBUG
                        Console.WriteLine($"[Compiler.CompileLambda] 跳过全局/内置: '{varName}'");
    #endif
                    }
                    else
                    {
                        // binding 为 null：变量不在当前作用域链中
                        // 可能是外层 Lambda Body 中的局部变量，强制分配
                        int captureIndex = _varTable.AllocCapture(varName);
                        captureSlots[varName] = captureIndex;

    #if DEBUG
                        Console.WriteLine($"[Compiler.CompileLambda] 强制分配捕获槽位（未知来源）: '{varName}' → captureSlot={captureIndex}");
    #endif
                    }
                }
            }

            // ===== 第 6 步：构建 captureMappings =====
            var captureMappings = captureSlots
                .Select(kv => (kv.Key, kv.Value))
                .ToList();

    #if DEBUG
            Console.WriteLine($"[Compiler.CompileLambda] captureMappings: [{string.Join(", ", captureMappings.Select(m => $"('{m.Key}', {m.Value})"))}]");
    #endif

            // ===== 第 7 步：发射 CreateClosure =====
            int chunkIndex = _chunk.RegisterClosure(closureChunk);
            Emit(OpCode.CreateClosure, (chunkIndex, expr.Params, captureMappings));
        }

        /// <summary>
        /// 递归收集嵌套闭包中所有 CaptureNames 中的变量名
        /// 不依赖 ResolveVariable，直接按名收集
        /// </summary>
        private static void CollectNestedCaptures(ByteCodeChunk chunk, HashSet<string> captureNames)
        {
            var vt = chunk.VariableTable;
            if (vt != null && vt.CaptureCount > 0)
            {
                foreach (var captureName in vt.CaptureNames.Keys)
                {
    #if DEBUG
                    if (!captureNames.Contains(captureName))
                        Console.WriteLine($"[CollectNestedCaptures] 从嵌套闭包中发现: '{captureName}'");
    #endif
                    captureNames.Add(captureName);
                }
            }

            // 始终递归进入更深层的嵌套闭包
            foreach (var nestedChunk in chunk.GetClosures())
            {
                CollectNestedCaptures(nestedChunk, captureNames);
            }
        }*/
    // ==================== 函数调用 ====================

    private void CompileCall(CallExpr expr)
    {
        Visit(expr.Target);
        foreach (var arg in expr.Args)
        {
            Visit(arg);
        }
        Emit(OpCode.Call, expr.Args.Count);
    }

    // ==================== 代码块 ====================

    private void CompileBlock(BlockExpr expr)
    {
        PushScope();

        for (int i = 0; i < expr.Statements.Count; i++)
        {
            Visit(expr.Statements[i]);
            if (i < expr.Statements.Count - 1)
            {
                Emit(OpCode.Pop);
            }
        }

        PopScope();
    }

    // ==================== 数组 ====================

    private void CompileArrayLiteral(ArrayLiteralExpr expr)
    {
        foreach (var element in expr.Elements)
        {
            Visit(element);
        }
        Emit(OpCode.CreateArray, expr.Elements.Count);
    }

    // ==================== 对象 ====================

    private void CompileObjectLiteral(ObjectLiteralExpr expr)
    {
        PushScope();
        DefineVariable("this", isMutable: true);

        for (int i = expr.Properties.Count - 1; i >= 0; i--)
        {
            var prop = expr.Properties[i];
            int propIndex = _chunk.AddConstant(prop.Key);
            Emit(OpCode.LoadConst, propIndex);
            Visit(prop.Value);
        }
        Emit(OpCode.CreateObject, expr.Properties.Count);
        PopScope();
    }

    // ==================== 成员访问 ====================

    private void CompileMemberAccess(MemberAccessExpr expr)
    {
        Visit(expr.Target);
        int propIndex = _chunk.AddConstant(expr.Property);

        if (expr.SafeNull)
        {
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

    private void CompileMemberAssign(MemberAssignExpr expr)
    {
        Visit(expr.Target);
        int propIndex = _chunk.AddConstant(expr.Property);
        Emit(OpCode.LoadConst, propIndex);
        Visit(expr.Value);

        if (expr.SafeNull)
        {
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

    // ==================== 索引访问 ====================

    private void CompileIndexAccess(IndexAccessExpr expr)
    {
        Visit(expr.Target);
        Visit(expr.Index);
        Emit(OpCode.GetIndex);
    }

    // ==================== Import ====================

    private void CompileImport(ImportStmt expr)
    {
        var importData = new List<object?> { expr.FilePath };

        foreach (var (member, alias) in expr.Members)
        {
            importData.Add(member);
            importData.Add(alias);
        }

        int dataIndex = _chunk.AddConstant(importData);
        Emit(OpCode.Import, dataIndex);

        var currentScope = _scopeStack.Peek();
        foreach (var (member, alias) in expr.Members)
        {
            var name = alias ?? member;

            GlobalSlotRegistry.Register(name);
            int globalIndex = _varTable.AllocGlobal(name);

            if (!currentScope.ContainsKey(name))
            {
                currentScope[name] = new VariableBinding(globalIndex, false)
                {
                    IsImported = true,
                    IsGlobal = true,
                    Region = SlotRegion.Global
                };
            }
        }
    }
}