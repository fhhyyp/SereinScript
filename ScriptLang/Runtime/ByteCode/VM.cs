using ScriptLang.Parser;
using ScriptLang.Prototype;
using ScriptLang.Runtime.ByteCode;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>
/// 字节码虚拟机
/// </summary>
public class VM
{
    private readonly ScriptEngine _engine;
    private readonly Stack<Value> _stack = new();
    private readonly Stack<CallFrame> _frames = new();
    private readonly Stack<Value> _iteratorStack = new();

    // 全局作用域（共享解释器的全局作用域）
    private Scope _globalScope;

    // 当前调用帧
    private CallFrame _currentFrame;


    // 内置函数注册表
    private readonly Dictionary<string, FunctionValue> _builtins;

    public VM(ScriptEngine engine)
    {
        _engine = engine;
        _globalScope = new Scope();
        _builtins = [];
        foreach (var func in BuiltinFunctions.FunctionCaches)
        {
            _globalScope.DefineFunction(func);
            _builtins[func.Name] = func;
        }
        
    }

    public async ValueTask<Value> ExecuteAsync(ByteCodeChunk chunk)
    {
#if DEBUG
        Console.WriteLine("=== 常量表 ===");
        var constants = chunk.GetConstants();
        foreach(var item in constants.Select((Value, Index) =>(Value, Index)))
        {
            int i = item.Index;
            object? constant = item.Value;
            Console.WriteLine($"  [{i}] = {constant}");
        }

        Console.WriteLine("=== 指令 ===");
        for (int i = 0; i < chunk.Code.Count; i++)
        {
            Console.WriteLine($"  {i:D4}: {chunk.Code[i].OpCode} {chunk.Code[i].Operand}");
        }
        Console.WriteLine("=== 执行 ===");

        var totalSw = Stopwatch.StartNew();
        var instructionTimes = new Dictionary<OpCode, long>();
#endif

        //_chunk = chunk;
        //_ip = 0;
        _stack.Clear();
        _frames.Clear();

        _currentFrame = new CallFrame
        {
            Chunk = chunk,
            Closure = _globalScope,
            //ReturnAddress = -1  ,// 顶层帧没有返回地址
            IP = 0,
        };
       
        while (_currentFrame.IP >= 0 && _currentFrame.IP < _currentFrame.Chunk.Code.Count)
        {
            int currentIP = _currentFrame.IP;
            var inst = _currentFrame.Chunk.Code[currentIP];
#if DEBUG
            var sw = Stopwatch.StartNew();
            Console.WriteLine($"[VM] IP={currentIP:D4}: {inst.OpCode} {inst.Operand}");
#endif

            try
            {
                bool shouldContinue = await ExecuteInstruction(inst);
#if DEBUG
                sw.Stop();
                if (!instructionTimes.ContainsKey(inst.OpCode))
                    instructionTimes[inst.OpCode] = 0;
                instructionTimes[inst.OpCode] += sw.ElapsedTicks;
#endif
                // 如果 ExecuteInstruction 返回 false，说明是 Return 指令
                // 在顶层 Return 时直接返回
                if (!shouldContinue)
                {
                    return _stack.Count > 0 ? Pop() : Value.Null;
                }
            }
            catch (Exception ex)
            {
                throw new RuntimeException($"执行错误 at {_currentFrame.IP}: {inst.OpCode} - {ex.Message}");
            }
            // 只有非跳转、非返回、非调用指令才自动 IP++
            if (!IsControlFlowInstruction(inst.OpCode))
            {
                _currentFrame.IP++;
            }
        }
#if DEBUG
        totalSw.Stop();
        Console.WriteLine($"总耗时: {totalSw.ElapsedMilliseconds}ms");
        foreach (var kv in instructionTimes.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"  {kv.Key}: {kv.Value * 1000 / Stopwatch.Frequency}ms");
        }
#endif

        return _stack.Count > 0 ? Pop() : Value.Null;
    }

    /// <summary>
    /// 判断是否为控制流指令（这些指令自己管理 IP）
    /// </summary>
    private static bool IsControlFlowInstruction(OpCode op)
    {
        return op switch
        {
            OpCode.Jmp or OpCode.JumpIfTrue or OpCode.JmpIfFalse or
            OpCode.Return or
            OpCode.Call or
            OpCode.MoveNext => true,  // MoveNext 失败时会跳转
            _ => false
        };
    }

    /// <summary>
    /// 执行单条指令
    /// </summary>
    /// <returns>true 继续执行，false 停止 VM</returns>
    private async Task<bool> ExecuteInstruction(Instruction inst)
    {
        switch (inst.OpCode)
        {
            // ===== 常量加载 =====
            case OpCode.Nop:
                return true;

            case OpCode.LoadNull:
                Push(Value.Null);
                return true;

            case OpCode.LoadTrue:
                Push(BoolValue.True);
                return true;

            case OpCode.LoadFalse:
                Push(BoolValue.False);
                return true;

            case OpCode.LoadM1:
                Push(NumberValueCache.Int32_M1);
                return true;

            case OpCode.Load0:
                Push(NumberValueCache.Int32_0);
                return true;

            case OpCode.Load1:
                Push(NumberValueCache.Int32_1);
                return true;

            case OpCode.LoadConst:
                if(inst.Operand is int @index)
                {
                    LoadConstant(@index);
                }
                else
                {
                    var errorValue = inst.Operand?.ToString();
                }
                return true;

            case OpCode.LoadGlobal:
                LoadGlobal((string)inst.Operand!);
                return true;

            case OpCode.StoreGlobal:
                StoreGlobal((string)inst.Operand!);
                return true;

            // ===== 变量操作 =====
            case OpCode.LoadVar:
                LoadVar((string)inst.Operand!);
                return true;

            case OpCode.StoreVar:
                StoreVar((string)inst.Operand!);
                return true;

            case OpCode.Pop:
                Pop();
                return true;

            case OpCode.Dup:
                Push(Peek());
                return true;

            // ===== 算术运算 =====
            case OpCode.Add:
                BinaryOp(Add);
                return true;

            case OpCode.Sub:
                BinaryOp(Sub);
                return true;

            case OpCode.Mul:
                BinaryOp(Mul);
                return true;

            case OpCode.Div:
                BinaryOp(Div);
                return true;

            case OpCode.Mod:
                BinaryOp(Mod);
                return true;

            case OpCode.Neg:
                UnaryOp(Neg);
                return true;

            case OpCode.Not:
                UnaryOp(v => BoolValue.Create(!IsTrue(v)));
                return true;

            // ===== 比较运算 =====
            case OpCode.Equal:
                BinaryOp((l, r) => BoolValue.Create(IsEqual(l, r)));
                return true;

            case OpCode.Ne:
                BinaryOp((l, r) => BoolValue.Create(!IsEqual(l, r)));
                return true;

            case OpCode.Gt:
                BinaryOp((l, r) => Compare(l, r, CompareKind.Gt));
                return true;

            case OpCode.Ge:
                BinaryOp((l, r) => Compare(l, r, CompareKind.Ge));
                return true;

            case OpCode.Lt:
                BinaryOp((l, r) => Compare(l, r, CompareKind.Lt));
                return true;

            case OpCode.Le:
                BinaryOp((l, r) => Compare(l, r, CompareKind.Le));
                return true;

            case OpCode.And:
                And();
                return true;

            case OpCode.Or:
                Or();
                return true;

            // ===== 跳转指令 =====
            case OpCode.Jmp:
                JumpTo((int)inst.Operand!);
                return true;

            case OpCode.JumpIfTrue:
                if (IsTrue(Peek()))
                {
                    JumpTo((int)inst.Operand!);
                }
                else
                {
                    _currentFrame.IP++;
                }
                return true;

            case OpCode.JmpIfFalse:
                if (!IsTrue(Peek()))
                {
                    JumpTo((int)inst.Operand!);
                }
                else
                {
                    _currentFrame.IP++;
                }
                return true;

            // ===== 导入模块 =====
            case OpCode.Import:
                await ImportModule(inst.Operand!);
                return true;

            // ===== 函数和闭包 =====
            case OpCode.CreateClosure:
                CreateClosure(inst.Operand!);
                return true;

            case OpCode.Call:
                await CallAsync((int)inst.Operand!);
                return true;
            case OpCode.Return:
                return HandleReturn();

            // ===== 捕获变量 =====
            case OpCode.Capture:
                Capture((string)inst.Operand!);
                return true;

            case OpCode.LoadCapture:
                LoadCapture((int)inst.Operand!);
                return true;

            case OpCode.StoreCapture:
                StoreCapture((int)inst.Operand!);
                return true;

            // ===== 对象操作 =====
            case OpCode.CreateObject:
                CreateObject((int)inst.Operand!);
                return true;

            case OpCode.GetMember:
                GetMember();
                return true;

            case OpCode.SetMember:
                SetMember();
                return true;

            // ===== 数组操作 =====
            case OpCode.CreateArray:
                CreateArray((int)inst.Operand!);
                return true;

            case OpCode.GetIndex:
                GetIndex();
                return true;

            case OpCode.SetIndex:
                SetIndex();
                return true;

            // ===== 迭代器 =====
            case OpCode.GetIterator:
                GetIterator();
                return true;

            case OpCode.MoveNext:
                MoveNext();  // Push true/false，不关心返回值
                _currentFrame.IP++;  // 总是执行下一条（JmpIfFalse）
                return true;
            /*if (!MoveNext())
            {
                if(inst.Operand is int moveIndex)
                {
                    JumpTo(moveIndex);
                }
                else
                {
                    throw new Exception("Invalid move index");
                }
            }
            else
            {
                _currentFrame.IP++;
            }*/

            case OpCode.Current:
                Current();
                return true;

            default:
                throw new InvalidOperationException($"未知的字节码指令: {inst.OpCode}");
        }
    }


    /// <summary>
    /// 处理 Return 指令
    /// </summary>
    /// <returns>true 继续执行，false 表示顶层返回</returns>
    private bool HandleReturn()
    {
        var returnValue = Pop();
#if DEBUG
        Console.WriteLine($"[VM] HandleReturn: 帧数={_frames.Count}");
#endif
        if (_frames.Count == 0)
        {
            Push(returnValue);
            return false;
        }
        else
        {
            _currentFrame = _frames.Pop();


            Push(returnValue);
            return true;
        }


    }

    // ==================== 栈操作 ====================
    /// <summary>
    /// 跳转到指定位置（设置 _ip，跳过 _ip++）
    /// </summary>
    private void JumpTo(int targetIP)
    {
#if DEBUG
        Console.WriteLine($"  -> 跳转到 {targetIP}");
#endif
        //_ip = targetIP - 1; // 主循环会 _ip++，所以减 1

        _currentFrame.IP = targetIP;
    }

    private void Push(Value value) => _stack.Push(value);

    private Value Pop() => _stack.Pop();

    private Value Peek() => _stack.Peek();

    private void UnaryOp(Func<Value, Value> op)
    {
        Push(op(Pop()));
    }

    private void BinaryOp(Func<Value, Value, Value> op)
    {
        var right = Pop();
        var left = Pop();
        Push(op(left, right));
    }

    // ==================== 变量操作 ====================

    private void LoadVar(string name)
    {
        // 1. 局部变量
        if (_currentFrame.Locals.TryGetValue(name, out var local))
        {
            Push(local);
            return;
        }

        // 2. 捕获的变量
        if (_currentFrame.CapturedVariables.TryGetValue(name, out var capturedVar))
        {
            Push(capturedVar.Cell.Value);
            return;
        }

        // 3. 闭包捕获的变量
        if (_currentFrame.Closure.TryGetValue(name, out var closureInfo))
        {
            Push(closureInfo.Cell.Value);
            return;
        }

        //  4. 查找 this 对象的属性

        if (TryGetThisProperty(name, out var thisProperty))
        {
            Push(thisProperty);
            return;
        }

        // 5. VM 全局作用域
        if (_globalScope.TryGetValue(name, out var globalInfo))
        {
            Push(globalInfo.Cell.Value);
            return;
        }

        // 6. 内置函数（快速查找）
        if (_builtins.TryGetValue(name, out var builtin))
        {
            Push(builtin);
            return;
        }

        throw new RuntimeException($"未定义的变量 '{name}'");
    }

    /// <summary>
    /// 尝试从 this 对象中获取属性
    /// </summary>
    private bool TryGetThisProperty(string name, [NotNullWhen(true)] out Value? value)
    {
        // 查找 this
        ObjectValue? thisObj = null;

        if (_currentFrame.Locals.TryGetValue("this", out var localThis) && localThis is ObjectValue obj1)
            thisObj = obj1;
        else if (_currentFrame.CapturedVariables.TryGetValue("this", out var capturedThis) && capturedThis.Cell.Value is ObjectValue obj2)
            thisObj = obj2;
        else if (_currentFrame.Closure.TryGetValue("this", out var closureThis) && closureThis.Cell.Value is ObjectValue obj3)
            thisObj = obj3;

        if (thisObj != null && thisObj.TryGetValue(name, out value))
            return true;

        value = null;
        return false;
    }
    private void StoreVar(string name)
    {
        var value = Pop();

        if (_currentFrame.CapturedVariables.TryGetValue(name, out var capturedInfo))
        {
            capturedInfo.Cell.Value = value;
        }

        if (_currentFrame.Locals.ContainsKey(name))
        {
            _currentFrame.Locals[name] = value;
        }
        else if (_currentFrame.Closure.TryGetValue(name, out var info))
        {
            info.Cell.Value = value;
        }
        else if (TrySetThisProperty(name, value))
        {
            Push(value);
            return;
        }
        else if (_globalScope.TryGetValue(name, out var globalInfo))
        {
            globalInfo.Cell.Value = value;
        }
        else
        {
            // 新变量定义
            _currentFrame.Locals[name] = value;
        }

        Push(value);
    }

    /// <summary>
    /// 尝试设置 this 对象的属性
    /// </summary>
    private bool TrySetThisProperty(string name, Value value)
    {
        ObjectValue? thisObj = null;

        if (_currentFrame.Locals.TryGetValue("this", out var localThis) && localThis is ObjectValue obj1)
            thisObj = obj1;
        else if (_currentFrame.CapturedVariables.TryGetValue("this", out var capturedThis) && capturedThis.Cell.Value is ObjectValue obj2)
            thisObj = obj2;

        if (thisObj != null)
        {
            thisObj.Set(name, value);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 加载全局变量（新增指令）
    /// </summary>
    private void LoadGlobal(string name)
    {
        // 直接查找全局作用域和内置函数
        if (_globalScope.TryGetValue(name, out var globalInfo))
        {
            Push(globalInfo.Cell.Value);
            return;
        }

        if (_builtins.TryGetValue(name, out var builtin))
        {
            Push(builtin);
            return;
        }

        /*if (_currentFrame.CapturedVariables.TryGetValue(name, out var variable))
        {
            
            return;
        }*/

        throw new RuntimeException($"未定义的全局变量 '{name}'");
    }

    /// <summary>
    /// 存储全局变量
    /// </summary>
    private void StoreGlobal(string name)
    {
        var value = Pop();

        if (_globalScope.Exists(name))
        {
            _globalScope.Set(name, value);
        }
        else
        {
            _globalScope.Define(name, value, isMutable: true);
        }

        Push(value);
    }

    private void LoadConstant(int index)
    {
        var constant = _currentFrame.Chunk.GetConstant(index);
        var value = ValueFromConstant(constant);
        Push(value);
        //Push(ValueFromConstant(constant));
    }

    // ==================== 闭包操作 ====================

    private void Capture(string varName)
    {
#if DEBUG
        Console.WriteLine($"[VM] 捕获变量: '{varName}'");
#endif
        // 标记变量为已捕获
        if (_currentFrame.Closure.TryGetValue(varName, out var closureInfo))
        {
            closureInfo.IsCaptured = true;
            _currentFrame.CapturedVariables[varName] = closureInfo;
            return;
        }


        // 检查是否在局部变量中
        if (_currentFrame.Locals.TryGetValue(varName, out var localValue))
        {
            // 创建一个 VariableCell 包装局部变量值
            var cell = new VariableCell(localValue);
            var info = new VariableInfo(cell, true) { IsCaptured = true };
            _currentFrame.CapturedVariables[varName] = info;
#if DEBUG
            Console.WriteLine($"[VM] Capture: 从 Locals 捕获 '{varName}' = {localValue}");
#endif
            return;
        }

        // 检查全局作用域
        if (_globalScope.TryGetValue(varName, out var globalInfo))
        {
            _currentFrame.CapturedVariables[varName] = globalInfo;
#if DEBUG
            Console.WriteLine($"[VM] Capture: 从 Global 捕获 '{varName}'");
#endif
            return;
        }
        var placeholderCell = new VariableCell(Value.Null);
        var placeholderInfo = new VariableInfo(placeholderCell, true) { IsCaptured = true };
        _currentFrame.CapturedVariables[varName] = placeholderInfo;

#if DEBUG
        Console.WriteLine($"[VM] Capture: '{varName}' 创建占位符（将在 StoreVar 时填充）");
#endif
    }

    private void LoadCapture(int index)
    {
        var captureList = _currentFrame.CapturedVariables.Values.ToList();
        if (index < 0 || index >= captureList.Count)
        {
            throw new RuntimeException($"无效的捕获变量索引: {index}");
        }

        Push(captureList[index].Cell.Value);
    }

    private void StoreCapture(int index)
    {
        var value = Pop();
        var captureList = _currentFrame.CapturedVariables.Values.ToList();

        if (index < 0 || index >= captureList.Count)
        {
            throw new RuntimeException($"无效的捕获变量索引: {index}");
        }

        captureList[index].Cell.Value = value;
        Push(value);
    }

    /// <summary>
    /// 导入模块
    /// </summary>
    private async Task ImportModule(object operand)
    {
        int dataIndex = (int)operand;
        var importData = (List<object?>)_currentFrame.Chunk.GetConstant(dataIndex);

        // 解析扁平数组：[path, member1, alias1, member2, alias2, ...]
        var filePath = (string)importData[0]!;

        // 提取成员名列表（用于 ImportResolver）
        var memberNames = new List<string>();
        var memberMappings = new List<(string member, string alias)>();

        for (int i = 1; i < importData.Count; i += 2)
        {
            var member = (string)importData[i]!;
            var alias = importData[i + 1] as string;
            var name = alias ?? member;

            memberNames.Add(member);
            memberMappings.Add((member, name));
        }

        // 解析模块
        var exports = await _engine.ImportResolver.ResolveAsync(filePath)
            ?? throw new RuntimeException($"无法导入模块 '{filePath}'，请检查文件路径");

        // 将导入的成员注入当前作用域
        foreach (var (member, name) in memberMappings)
        {
            if (!exports.TryGetValue(member, out var value))
            {
                throw new RuntimeException($"模块 '{filePath}' 中未找到导出的成员 '{member}'");
            }

            // 注入到当前帧的局部变量
            _currentFrame.Locals[name] = value;
        }

        // Import 语句返回 null
        Push(Value.Null);
    }

    private void CreateClosure(object operand)
    {
        var (chunkIndex, parameters, freeVariables) =
            ((int, List<string>, List<string>))operand;

        var closureChunk = _currentFrame.Chunk.GetClosure(chunkIndex);
        // var closureChunk = (ByteCodeChunk)_currentFrame.Chunk.Constants[chunkIndex];
        var captured = new Dictionary<string, VariableInfo>();

        foreach (var varName in freeVariables)
        {
            // 优先从 CapturedVariables 中获取（Capture 指令已经处理过）
            if (_currentFrame.CapturedVariables.TryGetValue(varName, out var capturedVar))
            {
                captured[varName] = capturedVar;
#if DEBUG
                Console.WriteLine($"[VM] CreateClosure: 使用已捕获变量 '{varName}'");
#endif
                continue;
            }

            // 检查闭包
            if (_currentFrame.Closure.TryGetValue(varName, out var closureInfo))
            {
                captured[varName] = closureInfo;
#if DEBUG
                Console.WriteLine($"[VM] CreateClosure: 从 Closure 捕获 '{varName}'");
#endif
                continue;
            }

            // 检查局部变量
            if (_currentFrame.Locals.TryGetValue(varName, out var localValue))
            {
                var cell = new VariableCell(localValue);
                var info = new VariableInfo(cell, true) { IsCaptured = true };
                captured[varName] = info;
                _currentFrame.CapturedVariables[varName] = info;
#if DEBUG
                Console.WriteLine($"[VM] CreateClosure: 从 Locals 捕获 '{varName}' = {localValue}");
#endif
                continue;
            }

            // ✅ 检查全局作用域
            if (_globalScope.TryGetValue(varName, out var globalInfo))
            {
                captured[varName] = globalInfo;
#if DEBUG
                Console.WriteLine($"[VM] CreateClosure: 从 Global 捕获 '{varName}'");
#endif
                continue;
            }
#if DEBUG
            Console.WriteLine($"[VM] 警告: CreateClosure 无法找到变量 '{varName}'");
#endif
        }

        var closure = new LightweightClosure(captured);
        var func = new CompiledFunctionValue(
            parameters,
            closureChunk,
            closure
        );

        Push(func);
    }

    private void CreateClosure2(object operand)
    {
        var (chunkIndex, parameters, freeVariables) =
            ((int, List<string>, List<string>))operand;

        var closureChunk = (ByteCodeChunk)_currentFrame.Chunk.GetConstant(chunkIndex);
        var captured = new Dictionary<string, VariableInfo>();

        foreach (var varName in freeVariables)
        {
            if (_currentFrame.Closure.TryGetValue(varName, out var info))
            {
                captured[varName] = info;
            }
        }

        var closure = new LightweightClosure(captured);
        var func = new CompiledFunctionValue(
            parameters,
            closureChunk,
            closure
        );

        Push(func);
    }

    // ==================== 函数调用 ====================

    private async Task CallAsync(int argCount)
    {
        _currentFrame.IP++;
        // 弹出参数
        var args = new List<Value>();
        for (int i = 0; i < argCount; i++)
        {
            args.Insert(0, Pop());
        }
        // 弹出函数
        var target = Pop();
#if DEBUG
        Console.WriteLine($"    目标: {target} 参数: [{string.Join(", ", args)}]");
#endif
        if (target is CompiledFunctionValue compiledFunc)
        {
            await CallCompiledFunctionAsync(compiledFunc, args);
        }
        else if (target is FunctionValue scriptFunc)
        {
            var result = await scriptFunc.CallAsync(_engine, args);
            Push(result);
        }
        else if (target is ClrMethodValue clrMethod)
        {
            await CallClrMethodAsync(clrMethod, args);
        }
    }


    private async Task CallCompiledFunctionAsync(CompiledFunctionValue func, List<Value> args)
    {
        // 创建新调用帧
        // ReturnAddress 设置为当前 IP（Call 指令的位置）
        // 返回时会恢复到这个位置，然后主循环 _ip++ 会指向下一条指令
        var newFrame = new CallFrame
        {
            Chunk = func.Chunk,
            Closure = func.Closure,
            //ReturnAddress = _  // 保存当前 IP（Call 指令的位置）
            IP = 0, 
        };

        // 绑定参数到局部变量
        for (int i = 0; i < func.Parameters.Count; i++)
        {
            var paramValue = i < args.Count ? args[i] : Value.Null;
            newFrame.Locals[func.Parameters[i]] = paramValue;
#if DEBUG
            Console.WriteLine($"[VM] 绑定参数: {func.Parameters[i]} = {paramValue}");
#endif
        }

        // 保存当前帧，切换到新帧
        _frames.Push(_currentFrame);
        _currentFrame = newFrame;
        //_ip = -1; // 设置为 -1，主循环 _ip++ 后从 0 开始
    }

    private async Task CallScriptFunctionAsync(FunctionValue func, List<Value> args)
    {

        // 调用脚本函数（复用现有的 FunctionValue.CallAsync）
        var result = await func.CallAsync(_engine, args);
        Push(result);
    }

    private async Task CallClrMethodAsync(ClrMethodValue method, List<Value> args)
    {

        // 转换参数为 CLR 对象
        var clrArgs = new object?[method.ParameterCount];
        var methodParams = method.Delegate.MethodInfo.GetParameters();

        for (int i = 0; i < Math.Min(args.Count, clrArgs.Length); i++)
        {
            clrArgs[i] = ConvertValueToClr(args[i], methodParams[i].ParameterType);
        }

        // 调用 CLR 方法
        var result = await method.InvokeAsync(clrArgs);

        // 转换返回值为脚本值
        Push(ConvertClrToValue(result));
    }

    /*private bool ReturnFromCall()
    {
        if (_frames.Count == 0)
        {
            return false; // 顶层返回
        }

        // 恢复上一个调用帧
        _currentFrame = _frames.Pop();
        _chunk = _currentFrame.Chunk;
        _ip = _currentFrame.ReturnAddress;

        return true;
    }*/

    // ==================== 对象和数组操作 ====================

    private void CreateObject(int propertyCount)
    { 
#if DEBUG
        Console.WriteLine($"[VM] CreateObject: propertyCount={propertyCount}, 栈深度={_stack.Count}");
#endif
        var properties = new Dictionary<string, Value>();

        // 从栈中弹出属性和值（成对出现）
        for (int i = 0; i < propertyCount; i++)
        {
            var value = Pop();
            var key = Pop();
            properties[key.AsString()] = value;
        }
        var obj = new ObjectValue(properties);
        // 将创建的对象绑定到 this（局部变量和捕获变量）
        if (_currentFrame.Locals.ContainsKey("this"))
            _currentFrame.Locals["this"] = obj;

        if (_currentFrame.CapturedVariables.TryGetValue("this", out var capturedThis))
            capturedThis.Cell.Value = obj;

        Push(obj);
    }

    private void GetMember()
    {
        var memberName = Pop().AsString();
        var target = Pop();

        if (target is ObjectValue obj)
        {
            if (obj.TryGetValue(memberName, out var value))
            {
                Push(value);
                return;
            }

            // 内置方法
            var builtinMethod = ObjectPrototype.GetMethod(obj, memberName);
            if (builtinMethod != null)
            {
                Push(builtinMethod);
                return;
            }
           
        }
        else if (target is ArrayValue arr)
        {
            var arrayMethod = ArrayPrototype.GetMethod(arr, memberName, _engine);
            if (arrayMethod != null)
            {
                Push(arrayMethod);
                return;
            }
        }
        else if (target is StringValue str)
        {
            var stringMethod = StringPrototype.GetMethod(str, memberName);
            if (stringMethod != null)
            {
                Push(stringMethod);
                return;
            }
        }
        else if (target is ClrObjectValue clrObj)
        {
            var clrMember = AccessClrMember(clrObj, memberName);
            if (clrMember != null)
            {
                Push(clrMember);
                return;
            }
        }

        throw new RuntimeException($"未找到成员 '{memberName}'");
    }

    private void SetMember()
    {
        var value = Pop();
        var memberName = Pop().AsString();
        var target = Pop();

        if (target is ObjectValue obj)
        {
            obj.Set(memberName, value);
        }
        else if (target is ClrObjectValue clrObj)
        {
            SetClrMember(clrObj, memberName, value);
        }
        else
        {
            throw new RuntimeException($"无法为 {target.GetType()} 设置成员 '{memberName}'");
        }

        Push(value);
    }

    private void CreateArray(int elementCount)
    {
        var elements = new List<Value>();

        // 从栈中弹出元素
        for (int i = 0; i < elementCount; i++)
        {
            elements.Insert(0, Pop()); // 逆序插入
        }
#if DEBUG
        Console.WriteLine($"[VM] CreateArray: [{string.Join(", ", elements)}]");
#endif
        Push(new ArrayValue(elements));
    }

    private void GetIndex()
    {
        var index = Pop();
        var target = Pop();
#if DEBUG
    Console.WriteLine($"[VM] GetIndex: target={target}, index={index}");
    if (target is ArrayValue arr_TEMP)
    {
        Console.WriteLine($"[VM]   arr.Elements.Count={arr_TEMP.Elements.Count}");
        for (int j = 0; j < arr_TEMP.Elements.Count; j++)
            Console.WriteLine($"[VM]   arr1[{j}]={arr_TEMP.Elements[j]}");
    }
#endif
        if (target is ArrayValue arr && index.IsNumber_Int)
        {
            int i = index.As<int>();
            if (i < 0 || i >= arr.Elements.Count)
                throw new RuntimeException($"数组索引越界: {i}");
            var value = arr.Get(i);
            Push(value);
        }
        else if (target is StringValue str && index.IsNumber_Int)
        {
            int i = index.As<int>();
            if (i < 0 || i >= str.Value.Length)
                throw new RuntimeException($"字符串索引越界: {i}");
            var value = str.Value[i].ToString();

            Push(new StringValue(value));
        }
        else if (target is ObjectValue obj && index is StringValue key)
        {
            if (obj.TryGetValue(key.Value, out var value))
                Push(value);
            else
                throw new RuntimeException($"对象中未找到键 '{key.Value}'");
        }
        else
        {
            throw new RuntimeException($"无效的索引访问");
        }
    }

    private void SetIndex()
    {
        var value = Pop();
        var index = Pop();
        var target = Pop();

        if (target is ArrayValue arr && index.IsNumber_Int)
        {
            int i = index.As<int>();
            if (i < 0 || i >= arr.Elements.Count)
                throw new RuntimeException($"数组索引越界: {i}");
            arr.Set(i, value, _engine);
        }
        else if (target is ObjectValue obj && index is StringValue key)
        {
            obj.Set(key.Value, value);
        }
        else
        {
            throw new RuntimeException($"无效的索引赋值");
        }

        Push(value);
    }

    // ==================== 迭代器操作 ====================

    private void GetIterator()
    {
        var iterable = Pop();

        if (iterable is ArrayValue arr)
        {
            _iteratorStack.Push(arr); // 迭代对象
            var index = NumberValue<int>.Create(0);
            _iteratorStack.Push(index); // 当前索引
            Push(BoolValue.True);
        }
        else
        {
            throw new RuntimeException("For 循环期望数组");
        }
    }


    private void MoveNext()
    {
        var indexValue = _iteratorStack.Peek();
        var index = indexValue.As<int>();
        var array = (ArrayValue)_iteratorStack.Skip(1).First();

#if DEBUG
        Console.WriteLine($"[VM] MoveNext: index={index}, count={array.Elements.Count}");
#endif

        if (index < array.Elements.Count)
        {
            Push(BoolValue.True);
            return;
        }
        else
        {
            _iteratorStack.Pop();
            _iteratorStack.Pop();
            Push(BoolValue.False);
            return;
        }
        
    }

    /* var indexValue = _iteratorStack.Peek();
         var index = indexValue.As<int>();
         var array = (ArrayValue)_iteratorStack.Skip(1).First();

         if (index < array.Elements.Count)
         {
             Push(BoolValue.True);
             return true;
         }
         _iteratorStack.Pop(); // 弹出索引
         _iteratorStack.Pop(); // 弹出数组

         Push(BoolValue.False);
         return false;*/


    private void Current()
    {
        var indexValue = _iteratorStack.Peek();
        var index = indexValue.As<int>();
        var array = (ArrayValue)_iteratorStack.Skip(1).First();

        if (index < array.Elements.Count)
        {
            Push(array.Get(index));
            // 递增索引
            _iteratorStack.Pop();
            var newIndex = NumberValue<int>.Create(index + 1);
            _iteratorStack.Push(newIndex);
        }
        else
        {
            Push(Value.Null);
        }
    }

    // ==================== 短路求值 ====================

    private void And()
    {
        // && 的短路求值在编译器层面通过跳转实现
        // 这里处理栈上的两个值
        var right = Pop();
        var left = Pop();
        Push(BoolValue.Create(IsTrue(left) && IsTrue(right)));
    }

    private void Or()
    {
        // || 的短路求值在编译器层面通过跳转实现
        var right = Pop();
        var left = Pop();
        Push(BoolValue.Create(IsTrue(left) || IsTrue(right)));
    }

    // ==================== 算术运算 ====================

    private static Value Add(Value left, Value right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return NumberOp(left, right,
                (a, b) => a + b,
                (a, b) => a + b,
                (a, b) => a + b,
                (a, b) => a + b,
                (a, b) => a + b);
            //Console.WriteLine($"{left} + {right} = " + result);
           
        }

        if (left.IsString || right.IsString)
            return new StringValue(left.AsString() + right.AsString());

        if (left is ArrayValue leftArr)
        {
            var newArray = left.AsArray().ToList();  // Array.Copy 优化
            if (right.IsArray)
                newArray.AddRange(right.AsArray());
            else
                newArray.Add(right);
            return new ArrayValue(newArray);
        }

        throw new RuntimeException($"不支持的操作: {left} + {right}");
    }

    private static Value Sub(Value left, Value right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return NumberOp(left, right,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b);
        }

        if (left.IsArray)
        {
            var arr = left.AsArray().ToList();
            if (right.IsArray)
            {
                var rightArr = right.AsArray();
                arr.RemoveAll(x => rightArr.Contains(x));
            }
            else
            {
                arr.Remove(right);
            }
            return new ArrayValue(arr);
        }

        throw new RuntimeException($"不支持的操作: {left} - {right}");
    }

    private static Value Mul(Value left, Value right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return NumberOp(left, right,
                (a, b) => a * b,
                (a, b) => a * b,
                (a, b) => a * b,
                (a, b) => a * b,
                (a, b) => a * b);
        }

        if (left.IsString && right.IsNumber_Int)
        {
            var str = left.AsString();
            var repeat = right.As<int>();
            return new StringValue(string.Concat(Enumerable.Repeat(str, repeat)));
        }

        throw new RuntimeException($"不支持的操作: {left} * {right}");
    }

    private static Value Div(Value left, Value right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return NumberOpDiv(left, right);
        }

        throw new RuntimeException($"不支持的操作: {left} / {right}");
    }

    private static Value Mod(Value left, Value right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return NumberOp(left, right,
                (a, b) => a % b,
                (a, b) => a % b,
                (a, b) => a % b,
                (a, b) => a % b,
                (a, b) => a % b);
        }

        throw new RuntimeException($"不支持的操作: {left} % {right}");
    }

    private static Value Neg(Value value)
    {
        if (value.IsNumber)
        {
            if (value.IsNumber_Decimal) return NumberValue<decimal>.Create(-value.As<decimal>());
            if (value.IsNumber_Double) return NumberValue<double>.Create(-value.As<double>());
            if (value.IsNumber_Float) return NumberValue<float>.Create(-value.As<float>());
            if (value.IsNumber_Long) return NumberValue<long>.Create(-value.As<long>());
            if (value.IsNumber_Int) return NumberValue<int>.Create(-value.As<int>());
        }

        throw new RuntimeException($"不支持的操作: -{value}");
    }

    private static Value NumberOp(
        Value left, Value right,
        Func<decimal, decimal, decimal> decOp,
        Func<double, double, double> dblOp,
        Func<float, float, float> fltOp,
        Func<long, long, long> lngOp,
        Func<int, int, int> intOp)
    {
        if (left.IsNumber_Decimal || right.IsNumber_Decimal)
            return NumberValue<decimal>.Create(decOp(left.As<decimal>(), right.As<decimal>()));
        if (left.IsNumber_Double || right.IsNumber_Double)
            return NumberValue<double>.Create(dblOp(left.As<double>(), right.As<double>()));
        if (left.IsNumber_Float || right.IsNumber_Float)
            return NumberValue<float>.Create(fltOp(left.As<float>(), right.As<float>()));
        if (left.IsNumber_Long || right.IsNumber_Long)
            return NumberValue<long>.Create(lngOp(left.As<long>(), right.As<long>()));
        return NumberValue<int>.Create(intOp(left.As<int>(), right.As<int>()));
    }
    /// <summary>
    /// 除法专用的类型提升（整数自动提升为 double）
    /// </summary>
    private static Value NumberOpDiv(Value left, Value right)
    {
        // decimal 优先级最高
        if (left.IsNumber_Decimal || right.IsNumber_Decimal)
            return NumberValue<decimal>.Create(left.As<decimal>() / right.As<decimal>());

        // 有一方是浮点，用 double
        if (left.IsNumber_Double || right.IsNumber_Double)
            return NumberValue<double>.Create(left.As<double>() / right.As<double>());

        if (left.IsNumber_Float || right.IsNumber_Float)
            return NumberValue<float>.Create(left.As<float>() / right.As<float>());

        // 整数除法：提升为 double
        return NumberValue<double>.Create(left.As<double>() / right.As<double>());
    }

    // ==================== 比较运算 ====================

    private enum CompareKind { Lt, Le, Gt, Ge }

    private static Value Compare(Value left, Value right, CompareKind kind)
    {
        if (left.IsNumber && right.IsNumber)
        {
            int cmp = CompareNumbers(left, right);
            return BoolValue.Create(kind switch
            {
                CompareKind.Lt => cmp < 0,
                CompareKind.Le => cmp <= 0,
                CompareKind.Gt => cmp > 0,
                CompareKind.Ge => cmp >= 0,
                _ => false
            });
        }

        if (left.IsString && right.IsString)
        {
            int cmp = string.Compare(left.AsString(), right.AsString());
            return BoolValue.Create(kind switch
            {
                CompareKind.Lt => cmp < 0,
                CompareKind.Le => cmp <= 0,
                CompareKind.Gt => cmp > 0,
                CompareKind.Ge => cmp >= 0,
                _ => false
            });
        }

        throw new RuntimeException($"不支持比较: {left} 和 {right}");
    }

    private static int CompareNumbers(Value left, Value right)
    {
        if (left.IsNumber_Decimal || right.IsNumber_Decimal)
            return left.As<decimal>().CompareTo(right.As<decimal>());
        if (left.IsNumber_Double || right.IsNumber_Double)
            return left.As<double>().CompareTo(right.As<double>());
        if (left.IsNumber_Float || right.IsNumber_Float)
            return left.As<float>().CompareTo(right.As<float>());
        if (left.IsNumber_Long || right.IsNumber_Long)
            return left.As<long>().CompareTo(right.As<long>());
        return left.As<int>().CompareTo(right.As<int>());
    }

    // ==================== 辅助方法 ====================

    private static bool IsTrue(Value value)
    {
        return value switch
        {
            NullValue => false,
            BoolValue b => b.Value,
            StringValue s => s.Value.Length > 0,
            ArrayValue a => a.Elements.Count > 0,
            ObjectValue o => o.Properties.Count > 0,
            _ => false,
        };
    }

    private static bool IsEqual(Value left, Value right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return CompareNumbers(left, right) == 0;
        }

        return (left, right) switch
        {
            (NullValue, NullValue) => true,
            (BoolValue bl, BoolValue br) => bl.Value == br.Value,
            (StringValue sl, StringValue sr) => sl.Value == sr.Value,
            (ArrayValue al, ArrayValue ar) =>
                al.Elements.Count == ar.Elements.Count &&
                al.Elements.Zip(ar.Elements).All(p => IsEqual(p.First, p.Second)),
            (ObjectValue ol, ObjectValue or) =>
                ol.Properties.Count == or.Properties.Count &&
                ol.Properties.All(kv =>
                    or.Properties.TryGetValue(kv.Key, out var v) && IsEqual(kv.Value, v)),
            _ => left.Equals(right),
        };
    }

    private static Value ValueFromConstant(object? constant)
    {
        return constant switch
        {
            null => Value.Null,
            int i => NumberValue<int>.Create(i),
            long l => NumberValue<long>.Create(l),
            float f => NumberValue<float>.Create(f),
            double d => NumberValue<double>.Create(d),
            decimal m => NumberValue<decimal>.Create(m),
            string s => new StringValue(s),
            bool b => BoolValue.Create(b),
            _ => throw new RuntimeException($"不支持的常量类型: {constant?.GetType()}")
        };
    }

    private static object? ConvertValueToClr(Value value, Type targetType)
    {
        if (value.IsNull) return null;
        if (value.IsString) return value.AsString();
        if (value.IsBool) return value.AsBool();
        if (value.IsNumber_Int) return value.As<int>();
        if (value.IsNumber_Long) return value.As<long>();
        if (value.IsNumber_Float) return value.As<float>();
        if (value.IsNumber_Double) return value.As<double>();
        if (value is ClrObjectValue clr) return clr.ClrObject;

        throw new RuntimeException($"无法转换 {value.GetType()} 到 CLR 类型");
    }

    private static Value ConvertClrToValue(object? clrValue)
    {
        return clrValue switch
        {
            null => Value.Null,
            int i => NumberValue<int>.Create(i),
            long l => NumberValue<long>.Create(l),
            float f => NumberValue<float>.Create(f),
            double d => NumberValue<double>.Create(d),
            string s => new StringValue(s),
            bool b => BoolValue.Create(b),
            _ => new ClrObjectValue(clrValue)
        };
    }

 
    // ==== CLR 互操作 ===

    private static Value? AccessClrMember(ClrObjectValue clrObj, string memberName)
    {
        var type = clrObj.ClrObject!.GetType();
        var prop = type.GetProperty(memberName);

        if (prop != null)
        {
            var value = prop.GetValue(clrObj.ClrObject);
            return ConvertClrToValue(value);
        }

        return null;
    }

    private static void SetClrMember(ClrObjectValue clrObj, string memberName, Value value)
    {
        var type = clrObj.ClrObject!.GetType();
        var prop = type.GetProperty(memberName);

        if (prop != null && prop.CanWrite)
        {
            var clrValue = ConvertValueToClr(value, prop.PropertyType);
            prop.SetValue(clrObj.ClrObject, clrValue);
        }
        else
        {
            throw new RuntimeException($"无法设置 CLR 对象的属性 '{memberName}'");
        }
    }
}

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

/// <summary>
/// 编译后的函数值
/// </summary>
public record CompiledFunctionValue(
    List<string> Parameters,
    ByteCodeChunk Chunk,
    IClosureContext Closure
) : Value
{
    public override T As<T>()
    {
        if (this is T result) return result;
        throw new InvalidCastException($"Cannot cast CompiledFunctionValue to {typeof(T)}");
    }
}