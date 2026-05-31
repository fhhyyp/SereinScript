using ScriptLang.Lexer;
using ScriptLang.Parser;
using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ScriptLang.Runtime;


/// <summary>
/// 解释器（执行器）
/// </summary>
public class Interpreter
{
    private readonly ImportResolver _importResolver;
    private readonly SourceManager _sourceManager;
    private readonly Scope _globalScope;
    private readonly ScriptEngine _engine;

    public Interpreter(ScriptEngine engine)
    {
        _importResolver = engine.ImportResolver;
        _sourceManager = engine.SourceManager;
        _globalScope = engine.GlobalScope;
        _engine = engine;
    }


    /// <summary>
    /// 异步执行主入口
    /// </summary>
    public async Task<EvalResult> EvaluateAsync(Expr expr, Scope scope)
    {
        if (expr is Parser.Program program)
            return await EvaluateProgramAsync(program, scope);
        if(expr is ReturnExpr returnExpr)
        {
            var t = await EvaluateReturnAsync(returnExpr, scope);
            return t;
        }
        
        var value = expr switch
        {
            //ReturnExpr returnExpr => await EvaluateReturnAsync(returnExpr, scope),
            LiteralExpr literal => EvaluateLiteral(literal),
            IdentifierExpr identifier => await EvaluateIdentifierAsync(identifier, scope),
            LetExpr let => await EvaluateLetAsync(let, scope),
            VarExpr var => await EvaluateVarAsync(var, scope),
            AssignExpr assign => await EvaluateAssignAsync(assign, scope),
            IndexAssignExpr indexAssign => await EvaluateIndexAssignAsync(indexAssign, scope),
            BinaryExpr binary => await EvaluateBinaryAsync(binary, scope),
            UnaryExpr unary => await EvaluateUnaryAsync(unary, scope),
            ConditionalExpr conditional => await EvaluateConditional(conditional, scope),
            IfExpr ifExpr => await EvaluateIfAsync(ifExpr, scope),
            WhenExpr whenExpr => await EvaluateWhenAsync(whenExpr, scope),
            ForExpr forExpr => await EvaluateForAsync(forExpr, scope),
            LambdaExpr lambda => EvaluateLambda(lambda, scope),
            CallExpr call => await EvaluateCallAsync(call, scope),
            BlockExpr block => await EvaluateBlockAsync(block, scope),
            ArrayLiteralExpr array => await EvaluateArrayAsync(array, scope),
            ObjectLiteralExpr obj => await EvaluateObjectAsync(obj, scope),
            MemberAccessExpr member => await EvaluateMemberAccessAsync(member, scope),
            MemberAssignExpr memberAssign => await EvaluateMemberAssignAsync(memberAssign, scope),
            IndexAccessExpr index => await EvaluateIndexAccessAsync(index, scope),
            ImportStmt import => await EvaluateImportAsync(import, scope),
            ErrorExpr error => EvaluateError(error, scope),
            _ => throw Error(expr, "未知的表达式类型")
        };
        var result = value.FormResult();
        return result;
        
    }

    /// <summary>
    /// 解析错误表达式
    /// </summary>
    /// <param name="error"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    private Value EvaluateError(ErrorExpr error, Scope scope)
    {
        throw Error(error, $"解析发生异常:{error.Message}");
    }


    // ==================== 基础表达式 ====================

    private async Task<EvalResult> EvaluateProgramAsync(Parser.Program program, Scope scope)
    {
        Value result = Value.Null;
        EvalResult evalResult = EvalResult.FormResult(result);

        foreach (var stmt in program.Statements)
        {
            evalResult = await EvaluateAsync(stmt, scope);
            if (evalResult.HasValue)
                result = evalResult.Value;
            if (evalResult.IsReturn)
            {
                break;
            }
        }
        return result.FormResult();
    }

    // ==================== Import 表达式 ====================

    /// <summary>
    /// 执行 Import 语句
    /// </summary>
    private async Task<Value> EvaluateImportAsync(ImportStmt import, Scope scope)
    {
        
        // 使用 ImportResolver 解析模块
        var exports = await _importResolver.ResolveAsync(import);

        // 将模块成员注入当前作用域
        ModuleInjector.InjectMembers(exports, import.Members, scope);

        // ImportStmt 返回 void
        return Value.Null;
    }

    // ==================== 基础表达式 ====================

    private Value EvaluateLiteral(LiteralExpr literal)
    {
        return literal.Value switch
        {
            null => Value.Null,
            byte @byte => NumberValue<byte>.Create(@byte),
            short @short => NumberValue<short>.Create(@short),
            int @int => NumberValue<int>.Create(@int),
            long @long => NumberValue<long>.Create(@long),
            //uint @uint => NumberValue<uint>.Create(@uint),
            //ulong @ulong => NumberValue<ulong>.Create(@ulong),
            float @float => NumberValue<float>.Create(@float),
            double @double => NumberValue<double>.Create(@double),
            decimal @decimal => NumberValue<decimal>.Create(@decimal),
            string s => new StringValue(s),
            bool b => BoolValue.Create(b),
            _ => throw Error(literal, $"不支持的字面量类型: {literal.Value?.GetType()}")
        };
    }

    private async Task<Value> EvaluateLetAsync(LetExpr let, Scope scope)
    {
        Value value = (await EvaluateAsync(let.Value, scope)).Value;
        scope.Define(let.Name, value, isMutable: false);
        return value;
    }

    private async Task<Value> EvaluateVarAsync(VarExpr varExpr, Scope scope)
    {
        Value value = (await EvaluateAsync(varExpr.Value, scope)).Value;
        scope.Define(varExpr.Name, value, isMutable: true);
        return value;
    }

    private async Task<Value> EvaluateAssignAsync(AssignExpr assign, Scope scope)
    {
        Value value = (await EvaluateAsync(assign.Value, scope)).Value;
        scope.Set(assign.Name, value);
        return value;
    }

    // ==================== 运算符 ====================

    private async Task<Value> EvaluateBinaryAsync(BinaryExpr binary, Scope scope)
    {
        Value left = (await EvaluateAsync(binary.Left, scope)).Value;
        Value right = (await EvaluateAsync(binary.Right, scope)).Value;

        // 数值运算
        if (binary.Op == "+")
        {
            if (left.IsNumber && right.IsNumber)
            {
                if(left.IsNumber_Decimal || right.IsNumber_Decimal)
                {
                    return NumberValue<decimal>.Create(left.As<decimal>() + right.As<decimal>());
                }
                else if(left.IsNumber_Double || right.IsNumber_Double)
                {
                    return NumberValue<double>.Create(left.As<double>() + right.As<double>());
                }
                else if (left.IsNumber_Float || right.IsNumber_Float)
                {
                    return NumberValue<float>.Create(left.As<float>() + right.As<float>());
                }
                else if (left.IsNumber_Long || right.IsNumber_Long)
                {
                    return NumberValue<long>.Create(left.As<long>() + right.As<long>());
                }
                else if (left.IsNumber_Int || right.IsNumber_Int)
                {
                    return NumberValue<int>.Create(left.As<int>() + right.As<int>());
                }
                return NumberValue<double>.Create(left.As<double>() + right.As<double>());
            }


            if (left.IsArray)
            {
                if (right.IsArray)
                {
                    var newArray = left.AsArray().ToList();
                     newArray.AddRange(right.AsArray());
                    return new ArrayValue(newArray);
                }
                else
                {
                    var newArray = left.AsArray().ToList();
                    newArray.Add(right);
                    return new ArrayValue(newArray); // return org //new ArrayValue(left.AsArray());
                }
            }
            if (left.IsString || right.IsString)
                return new StringValue(left.AsString() + right.AsString());


            //return new StringValue(left.AsString() + right.AsString());
        }

        if (binary.Op == "-")
        {
            if (left.IsNumber && right.IsNumber)
            {
                if (left.IsNumber_Decimal || right.IsNumber_Decimal)
                {
                    return NumberValue<decimal>.Create(left.As<decimal>() - right.As<decimal>());
                }
                else if (left.IsNumber_Float || right.IsNumber_Float)
                {
                    return NumberValue<float>.Create(left.As<float>() - right.As<float>());
                }
                else if (left.IsNumber_Long || right.IsNumber_Long)
                {
                    return NumberValue<long>.Create(left.As<long>() - right.As<long>());
                }
                else if (left.IsNumber_Int || right.IsNumber_Int)
                {
                    return NumberValue<int>.Create(left.As<int>() - right.As<int>());
                }
                return NumberValue<double>.Create(left.As<double>() - right.As<double>());
            }

            if (left.IsArray)
            {
                if (right.IsArray)
                {
                    var tempArray = right.AsArray();
                    var newArr = left.AsArray().Where(x => !tempArray.Contains(x)).ToList();
                    return new ArrayValue(newArr);
                }
                else
                {
                    var newArr = left.AsArray().ToList();
                    newArr.Remove(right);
                    return new ArrayValue(newArr);
                }
            }

        }

        if (binary.Op == "*")
        {
            if (left.IsNumber && right.IsNumber)
            {
                if (left.IsNumber_Decimal || right.IsNumber_Decimal)
                {
                    return NumberValue<decimal>.Create(left.As<decimal>() * right.As<decimal>());
                }
                else if (left.IsNumber_Double || right.IsNumber_Double)
                {
                    return NumberValue<double>.Create(left.As<double>() * right.As<double>());
                }
                else if (left.IsNumber_Float || right.IsNumber_Float)
                {
                    return NumberValue<float>.Create(left.As<float>() * right.As<float>());
                }
                else if (left.IsNumber_Long || right.IsNumber_Long)
                {
                    return NumberValue<long>.Create(left.As<long>() * right.As<long>());
                }
                else if (left.IsNumber_Int || right.IsNumber_Int)
                {
                    return NumberValue<int>.Create(left.As<int>() * right.As<int>());
                }
                return NumberValue<double>.Create(left.As<double>() * right.As<double>());
            }

            if (left.IsString)
            {
                var str = left.AsString();
                var repeat = right.As<int>();
                return new StringValue(string.Concat(Enumerable.Repeat(str, repeat)));
            }
        }

        if (binary.Op == "/")
        {
            if (left.IsNumber && right.IsNumber)
            {
                if (left.IsNumber_Decimal || right.IsNumber_Decimal)
                {
                    return NumberValue<decimal>.Create(left.As<decimal>() / right.As<decimal>());
                }
                else if (left.IsNumber_Double || right.IsNumber_Double)
                {
                    return NumberValue<double>.Create(left.As<double>() / right.As<double>());
                }
                else if (left.IsNumber_Float || right.IsNumber_Float)
                {
                    return NumberValue<float>.Create(left.As<float>() / right.As<float>());
                }
                else if (left.IsNumber_Long || right.IsNumber_Long)
                {
                    return NumberValue<long>.Create(left.As<long>() / right.As<long>());
                }
                else if (left.IsNumber_Int || right.IsNumber_Int)
                {
                    return NumberValue<int>.Create(left.As<int>() / right.As<int>());
                }
                return NumberValue<double>.Create(left.As<double>() / right.As<double>());
            }
        }

        if (binary.Op == "%")
        {
            if (left.IsNumber && right.IsNumber)
            {
                if (left.IsNumber_Decimal || right.IsNumber_Decimal)
                {
                    return NumberValue<decimal>.Create(left.As<decimal>() % right.As<decimal>());
                }
                else if (left.IsNumber_Float || right.IsNumber_Float)
                {
                    return NumberValue<float>.Create(left.As<float>() % right.As<float>());
                }
                else if (left.IsNumber_Long || right.IsNumber_Long)
                {
                    return NumberValue<long>.Create(left.As<long>() % right.As<long>());
                }
                else if (left.IsNumber_Int || right.IsNumber_Int)
                {
                    return NumberValue<int>.Create(left.As<int>() % right.As<int>());
                }
                return NumberValue<double>.Create(left.As<double>() % right.As<double>());
            }
        }

        // 比较运算
        if (binary.Op == "==")
            return BoolValue.Create(IsEqual(left, right));

        if (binary.Op == "!=")
            return BoolValue.Create(!IsEqual(left, right));

        if (binary.Op == "<")
        {
            if (left.IsNumber && right.IsNumber)
            {
                if (left.IsNumber_Decimal || right.IsNumber_Decimal)
                {
                    return BoolValue.Create(left.As<decimal>() < right.As<decimal>());
                }
                else if(left.IsNumber_Double || right.IsNumber_Double)
                {
                    return BoolValue.Create(left.As<double>() < right.As<double>());
                }
                else if (left.IsNumber_Float || right.IsNumber_Float)
                {
                    return BoolValue.Create(left.As<float>() < right.As<float>());
                }
                else if (left.IsNumber_Long || right.IsNumber_Long)
                {
                    return BoolValue.Create(left.As<long>() < right.As<long>());
                }
                else if (left.IsNumber_Int || right.IsNumber_Int)
                {
                    return BoolValue.Create(left.As<int>() < right.As<int>());
                }
                return BoolValue.Create(left.As<double>() < right.As<double>());
            }
            if (left.IsString && right.IsString)
                return BoolValue.Create(string.Compare(left.AsString(), right.AsString()) < 0);
        }

        if (binary.Op == "<=")
        {
            if (left.IsNumber && right.IsNumber)
            {
                if (left.IsNumber_Decimal || right.IsNumber_Decimal)
                {
                    return BoolValue.Create(left.As<decimal>() <= right.As<decimal>());
                }
                else if (left.IsNumber_Double || right.IsNumber_Double)
                {
                    return BoolValue.Create(left.As<double>() <= right.As<double>());
                }
                else if (left.IsNumber_Float || right.IsNumber_Float)
                {
                    return BoolValue.Create(left.As<float>() <= right.As<float>());
                }
                else if (left.IsNumber_Long || right.IsNumber_Long)
                {
                    return BoolValue.Create(left.As<long>() <= right.As<long>());
                }
                else if (left.IsNumber_Int || right.IsNumber_Int)
                {
                    return BoolValue.Create(left.As<int>() <= right.As<int>());
                }
                return BoolValue.Create(left.As<double>() <= right.As<double>());
            }
            if (left.IsString && right.IsString)
                return BoolValue.Create(string.Compare(left.AsString(), right.AsString()) <= 0);
        }

        if (binary.Op == ">")
        {
            if (left.IsNumber && right.IsNumber)
            {
                if (left.IsNumber_Decimal || right.IsNumber_Decimal)
                {
                    return BoolValue.Create(left.As<decimal>() > right.As<decimal>());
                }
                else if (left.IsNumber_Double || right.IsNumber_Double)
                {
                    return BoolValue.Create(left.As<double>() > right.As<double>());
                }
                else if (left.IsNumber_Float || right.IsNumber_Float)
                {
                    return BoolValue.Create(left.As<float>() > right.As<float>());
                }
                else if (left.IsNumber_Long || right.IsNumber_Long)
                {
                    return BoolValue.Create(left.As<long>() > right.As<long>());
                }
                else if (left.IsNumber_Int || right.IsNumber_Int)
                {
                    return BoolValue.Create(left.As<int>() > right.As<int>());
                }
                return BoolValue.Create(left.As<double>() > right.As<double>());
            }
            if (left.IsString && right.IsString)
                return BoolValue.Create(string.Compare(left.AsString(), right.AsString()) > 0);
        }

        if (binary.Op == ">=")
        {
            if (left.IsNumber && right.IsNumber)
            {
                if (left.IsNumber_Decimal || right.IsNumber_Decimal)
                {
                    return BoolValue.Create(left.As<decimal>() >= right.As<decimal>());
                }
                else if (left.IsNumber_Double || right.IsNumber_Double)
                {
                    return BoolValue.Create(left.As<double>() >= right.As<double>());
                }
                else if (left.IsNumber_Float || right.IsNumber_Float)
                {
                    return BoolValue.Create(left.As<float>() >= right.As<float>());
                }
                else if (left.IsNumber_Long || right.IsNumber_Long)
                {
                    return BoolValue.Create(left.As<long>() >= right.As<long>());
                }
                else if (left.IsNumber_Int || right.IsNumber_Int)
                {
                    return BoolValue.Create(left.As<int>() >= right.As<int>());
                }
                return BoolValue.Create(left.As<double>() >= right.As<double>());
            }
            if (left.IsString && right.IsString)
                return BoolValue.Create(string.Compare(left.AsString(), right.AsString()) >= 0);
        }

        // 逻辑运算（短路求值）
        if (binary.Op == "&&" || binary.Op == "||")
        {
            if (!IsTrue(left)) return BoolValue.Create(false);
            return BoolValue.Create(IsTrue(right)); 
        }

        throw Error(binary, $"不支持的操作符 '{binary.Op}'，操作数类型为 {left.GetType()} 和 {right.GetType()}");
    }

    private async Task<Value> EvaluateUnaryAsync(UnaryExpr unary, Scope scope)
    {
        Value operand = (await EvaluateAsync(unary.Expr, scope)).Value;

        if (unary.Op == "-")
        {
            if (operand.IsNumber)
            {
                if (operand.IsNumber_Decimal)
                {
                    return NumberValue<decimal>.Create(-operand.As<decimal>());
                }
                else if (operand.IsNumber_Double)
                {
                    return NumberValue<double>.Create(-operand.As<double>());
                }
                else if (operand.IsNumber_Float)
                {
                    return NumberValue<float>.Create(-operand.As<float>());
                }
                else if (operand.IsNumber_Long)
                {
                    return NumberValue<long>.Create(-operand.As<long>());
                }
                else if (operand.IsNumber_Int)
                {
                    return NumberValue<int>.Create(-operand.As<int>());
                }
                return NumberValue<double>.Create(-operand.As<double>());
            }
        }

        if (unary.Op == "!")
        {
            return BoolValue.Create(!IsTrue(operand));
        }

        throw Error(unary, $"不支持的一元操作符 '{unary.Op}'，操作数类型为 {operand.GetType()}");
    }

    private async Task<Value> EvaluateConditional(ConditionalExpr conditional, Scope scope)
    {
        Value cond = (await EvaluateAsync(conditional.Cond, scope)).Value;
        return IsTrue(cond) ? (await EvaluateAsync(conditional.Then, scope)).Value : (await EvaluateAsync(conditional.Else, scope)).Value;
    }

    // ==================== 控制流 ====================

    private async Task<EvalResult> EvaluateReturnAsync(ReturnExpr returnExpr, Scope scope)
    {
        if(returnExpr.Value is null)
        {
            return EvalResult.ReturnNotValue();
        }
        else
        {
            var result = await EvaluateAsync(returnExpr.Value, scope);
            return result.Value.Return();
        }
    }
    private async Task<Value> EvaluateIfAsync(IfExpr ifExpr, Scope scope)
    {
        Value cond = (await EvaluateAsync(ifExpr.Cond, scope)).Value;
        return IsTrue(cond) ? (await EvaluateAsync(ifExpr.Then, scope)).Value: (await EvaluateAsync(ifExpr.Else, scope)).Value;
    }

    private async Task<Value> EvaluateWhenAsync(WhenExpr whenExpr, Scope scope)
    {
        Value value = (await EvaluateAsync(whenExpr.Value, scope)).Value;

        foreach (var clause in whenExpr.Clauses)
        {
            // 如果 pattern 是表达式，求值并比较
            Value patternValue = (await EvaluateAsync(clause.Pattern, scope)).Value;
            if (IsEqual(value, patternValue))
            {
                return (await EvaluateAsync(clause.Body, scope)).Value;
            }
        }

        return Value.Null;
    }

    private async Task<Value> EvaluateForAsync(ForExpr forExpr, Scope scope)
    {
        Value iterable = (await EvaluateAsync(forExpr.Iterable, scope)).Value;
        Value result = Value.Null;

        if (iterable is ArrayValue array)
        {
            if (array.Elements.Count > 0)
            {
                for (int i = 0; i < array.Elements.Count; i++)
                {
                    var loopScope = scope.CreateChildScope();
                    Value? element = array.Elements[i];
                    loopScope.Define(forExpr.VarName, element);
                    var evalResult = await EvaluateAsync(forExpr.Body, loopScope);
                    result = evalResult.Value; 
                    if (evalResult.HasValue)
                    {
                        result = evalResult.Value;
                    }
                    if (evalResult.IsReturn)
                    {
                        break;
                    }
                }
            }
        }
        /*else if (iterable is ObjectValue obj)
        {
            var loopScope = scope.CreateChildScope();

            foreach (var (key, value) in obj.Properties)
            {
                loopScope.Define(forExpr.VarName, new StringValue(key));
                var evalResult = await EvaluateAsync(forExpr.Body, loopScope);
                result = evalResult.Value;
                if (evalResult.IsReturn)
                {
                    break;
                }
            }
        }*/
        else
        {
            throw Error(forExpr, "For 循环期望数组");
        }

        return result;
    }

    // ==================== 函数 ====================

    private Value EvaluateLambda(LambdaExpr lambda, Scope scope)
    {
        return new FunctionValue(lambda, scope);
        //return new FunctionValue(lambda.Params, lambda.Body, scope, optimize: true);
    }

    private async Task<Value> EvaluateCallAsync(CallExpr call, Scope scope)
    {
        var result = await EvaluateAsync(call.Target, scope);
        Value target = result.Value;

        var args = new List<Value>();
        foreach (var arg in call.Args)
        {
            var data = (await EvaluateAsync(arg, scope)).Value;
            args.Add(data);
        }

        // 处理 FunctionValue（DSL Lambda + 原生函数）
        if (target is FunctionValue func)
        {
            return await func.CallAsync(_engine, args);
        }

        // 处理 ClrMethodValue（CLR 方法调用）
        if (target is ClrMethodValue clrMethod)
        {
            return await CallClrMethodAsync(call, clrMethod, args);
        }

        throw Error(call, $"只能调用函数，实际得到 {target.GetType()}");
    }

    // =================/// <summary>
    /// 块表达式（返回最后一个表达式的值）
    /// </summary>
    private async Task<Value> EvaluateBlockAsync(BlockExpr block, Scope scope)
    {
        var blockScope = scope.CreateChildScope();
        Value result = Value.Null;

        foreach (var stmt in block.Statements)
        {
            var evalResult = await EvaluateAsync(stmt, blockScope); 
            if (evalResult.HasValue)
                result = evalResult.Value;
            if (evalResult.IsReturn)
            {
                break;
            }
        }
        return result;
    }

    // ==================== 数据结构 ====================

    /// <summary>
    /// 构造数组
    /// </summary>
    /// <param name="array"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    private async Task<Value> EvaluateArrayAsync(ArrayLiteralExpr array, Scope scope)
    {
        var elements = new List<Value>();
        foreach (var expr in array.Elements)
        {
            var item = (await EvaluateAsync(expr, scope)).Value;

            elements.Add(item);
        }
        return new ArrayValue(elements);
    }

   /// <summary>
   /// 构造对象
   /// </summary>
   /// <param name="obj"></param>
   /// <param name="scope"></param>
   /// <returns></returns>
    private async Task<Value> EvaluateObjectAsync(ObjectLiteralExpr obj, Scope scope)
    {
        // 第一阶段：创建空对象外壳
        var objValue = new ObjectValue(new Dictionary<string, Value>());



        // 创建增强的作用域，包含对象自身的引用
        var objectScope = scope.CreateChildScope();
        objectScope.Define("this", objValue, isMutable: false);  // 提供this引用

        // 先占位：为所有属性创建占位符，避免"未定义"错误
        foreach (var prop in obj.Properties)
        {
            if (!objectScope.IsDefinedLocally(prop.Key))
            {
                objectScope.Define(prop.Key, Value.Null, isMutable: true);  // 占位符
            }
        }


        // 第二阶段：逐个求值并更新
        foreach (var prop in obj.Properties)
        {
            var key = prop.Key;

            if (scope.TryGetValue(key, out var var))
            {
                // 更新对象属性
                objValue.Set(key, var.Value);
                if (objectScope.IsDefinedLocally(key))
                {
                    objectScope.Set(key, var.Value);
                }
            }
            else
            {
                var result = await EvaluateAsync(prop.Value, objectScope);
                objValue.Set(key, result.Value);
            }
            /*else
            {
               
            }*/


            // 更新作用域中的占位符（使后续属性可以引用前面的属性）
            
        }

        return objValue;

        /*var properties = new Dictionary<string, Value>();
        foreach (var prop in obj.Properties)
        {
            var key = prop.Key;
            var value = (await EvaluateAsync(prop.Value, scope)).Value;
            properties[prop.Key] = value;
        }
        var objValue = new ObjectValue(properties);
        return objValue;*/
    }

    /// <summary>
    /// 访问成员
    /// </summary>
    /// <param name="member"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    private async Task<Value> EvaluateMemberAccessAsync(MemberAccessExpr member, Scope scope)
    {
        Value target = (await EvaluateAsync(member.Target, scope)).Value;

        if (target.IsNull && member.SafeNull)
        {
            return Value.Null;
        }

        // 处理 ObjectValue（脚本对象）
        if (target is ObjectValue obj)
        {
            if (obj.TryGetValue(member.Property, out var memberValue))
            {
                return memberValue;
            }

            #region 原生map对象方法
            return member.Property switch
            {
                "keys" => new ArrayValue(obj.Properties.Keys.Select(k => new StringValue(k)).Cast<Value>().ToList()),
                "values" => new ArrayValue(obj.Properties.Values.ToList()),
                "has" => new FunctionValue("has", args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "has() 期望 1 个参数");

                    if (args[0] is not StringValue stringValue)
                        throw Error(member, "has() 期望字符串");

                    var isContainsKey = obj.Properties.ContainsKey(stringValue.AsString());
                    return BoolValue.Create(isContainsKey);
                }),
                _ => throw Error(member, $"对象上找不到属性 '{member.Property}'"),
            };
            #endregion
        }

        // 处理 ArrayValue（数组）
        if (target is ArrayValue arr)
        {
            #region 原生数组方法
            return member.Property switch
            {
                "count" => NumberValue<int>.Create(arr.Elements.Count),
                "length" => NumberValue<int>.Create(arr.Elements.Count),
                "copy" => new ArrayValue(arr.Elements.Select(x => x).ToList()),
                "select" => new FunctionValue(
                    "select",
                    async args =>
                    {
                        if (args.Count != 1) throw Error(member, "select() 期望 1 个参数");
                        if (args[0] is not FunctionValue func) throw Error(member, "select() 期望一个函数");

                        var result = new List<Value>();
                        foreach (var item in arr.Elements)
                        {
                            var callResult = await func.CallAsync(_engine, item);
                            result.Add(callResult);
                        }
                        return new ArrayValue(result);
                    }),
                "where" => new FunctionValue(
                    "where",
                    async args =>
                    {
                        if (args.Count != 1) throw Error(member, "where() 期望 1 个参数");
                        if (args[0] is not FunctionValue func) throw Error(member, "where() 期望一个函数");

                        var result = new List<Value>();
                        foreach (var item in arr.Elements)
                        {
                            var callResult = await func.CallAsync(_engine, item);
                            if (IsTrue(callResult))
                                result.Add(item);
                        }
                        return new ArrayValue(result);
                    }),
                "forEach" => new FunctionValue(
                    "forEach",
                    async args =>
                    {
                        if (args.Count != 1) throw Error(member, "forEach() 期望 1 个参数");
                        if (args[0] is not FunctionValue func) throw Error(member, "forEach() 期望一个函数");

                        foreach (var item in arr.Elements)
                        {
                            var result = await func.CallAsync(_engine, item);
                        }
                        return Value.Null;
                    }),
                "slice" => new FunctionValue(
                    "slice",
                    args =>
                    {
                        int start = args[0].As<int>();
                        int end = args.Count > 1 ? args[1].As<int>() : arr.Elements.Count;
                        start = Math.Clamp(start, 0, arr.Elements.Count);
                        end = Math.Clamp(end, 0, arr.Elements.Count);
                        var slice = arr.Elements.GetRange(start, end - start);
                        return new ArrayValue(slice);
                    }),
                "first" => new FunctionValue("first", args =>
                {
                    if (arr.Elements.Count == 0) return Value.Null;
                    return arr.Elements[0];
                }),
                "last" => new FunctionValue("last", args =>
                {
                    if (arr.Elements.Count == 0) return Value.Null;
                    return arr.Elements[^1];
                }),
                "remove" => new FunctionValue("remove", args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "remove() 期望 1 个参数");
                    var state = arr.Remove(args[0], _engine);
                    return BoolValue.Create(state);
                }),
                "removeAt" => new FunctionValue("removeAt", args =>
                {
                    if (args.Count != 1 || !args[0].IsNumber_Int)
                        throw Error(member, "removeAt() 期望一个数字");

                    var index = args[0].As<int>();
                    if (index < 0) index = arr.Length + index;
                    arr.RemoveAt(index, _engine);
                    return Value.Null;
                }),
                "pop" => new FunctionValue("pop", args =>
                {
                    return arr.Pop(_engine);
                }),
                "push" => new FunctionValue("push", args =>
                {
                    foreach (var item in args)
                        arr.Add(item, _engine);
                    return Value.Null;
                }),
                "reverse" => new FunctionValue("reverse", args =>
                {
                    arr.Reverse(_engine);
                    return arr;
                }),
                "find" => new FunctionValue("find", async args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "find() 期望 1 个参数");

                    if (args[0] is not FunctionValue func)
                        throw Error(member, "find() 期望一个函数作为参数");

                    foreach (var item in arr.Elements)
                    {
                        var result = await func.CallAsync(_engine, item);
                        if (IsTrue(result))
                            return item;
                    }

                    return Value.Null;
                }),
                "findIndex" => new FunctionValue("findIndex", async args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "findIndex() 期望 1 个参数");

                    if (args[0] is not FunctionValue func)
                        throw Error(member, "findIndex() 期望一个函数作为参数");

                    for (int i = 0; i < arr.Elements.Count; i++)
                    {
                        var result = await func.CallAsync(_engine, arr.Elements[i]);
                        if (IsTrue(result))
                            return NumberValue<int>.Create(i);
                    }

                    return NumberValue<int>.Create(-1);
                }),

                "any" => new FunctionValue("any", async args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "any() 期望 1 个参数");

                    if (args[0] is not FunctionValue func)
                        throw Error(member, "any() 期望一个函数作为参数");

                    foreach (var item in arr.Elements)
                    {
                        var result = await func.CallAsync(_engine, item);
                        if (IsTrue(result))
                            return BoolValue.Create(true);
                    }

                    return BoolValue.Create(false);
                }),

                "all" => new FunctionValue("all", async args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "all() 期望 1 个参数");

                    if (args[0] is not FunctionValue func)
                        throw Error(member, "all() 期望一个函数作为参数");

                    foreach (var item in arr.Elements)
                    {
                        var result = await func.CallAsync(_engine, item);
                        if (!IsTrue(result))
                            return BoolValue.Create(false);
                    }

                    return BoolValue.Create(true);
                }),

                "join" => new FunctionValue("join", args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "join() 期望 1 个参数");

                    if (args[0] is not StringValue stringValue)
                        throw Error(member, "join() 期望一个字符串作为参数");

                    var joinResult = string.Join(stringValue.AsString(), arr.Elements.Select(x => x.AsString()));
                    return new StringValue(joinResult);
                }),
                /*"onChanged" => new FunctionValue("onChanged", args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "onChanged() 期望 1 个参数");

                    if (args[0] is not FunctionValue functionValue)
                        throw Error(member, "onChanged() 期望函数");

                    arr.AddOnChanged(functionValue);

                    return Value.Null;
                }),*/
                _ => throw Error(member, $"未知的数组方法 '{member.Property}'")
            };
            #endregion
        }

        // 处理 StringValue（字符串）
        if (target is StringValue str)
        {

            #region 原生方法列表
            return member.Property switch
            {
                "length" => NumberValue<int>.Create(str.Value.Length),
                "toString" => new FunctionValue(
                    "toString",
                    args =>
                    {
                        return new StringValue(target.AsString());
                    }),
                "split" => new FunctionValue(
                    "split",
                    args =>
                    {
                        if (args.Count != 1) throw Error(member, "split() 期望 1 个参数");
                        var sep = args[0].As<StringValue>().Value;
                        var parts = str.Value.Split(sep);
                        return new ArrayValue(parts.Select(s => (Value)new StringValue(s)).ToList());
                    }),
                "substring" => new FunctionValue(
                    "substring",
                    args =>
                    {
                        if (args.Count < 1 || args.Count > 2)
                            throw Error(member, "substring() 期望 1 或 2 个参数");
                        int start = args[0].As<int>();
                        int length = args.Count == 2 ? args[1].As<int>() : str.Value.Length - start;
                        return new StringValue(str.Value.Substring(start, length));
                    }),
                "toUpper" => new FunctionValue(
                    "toUpper",
                    args =>
                    {
                        if (args.Count != 0) throw Error(member, "toUpper() 期望 0 个参数");
                        return new StringValue(str.Value.ToUpperInvariant());
                    }),
                "toLower" => new FunctionValue(
                    "toLower",
                    args =>
                    {
                        if (args.Count != 0) throw Error(member, "toLower() 期望 0 个参数");
                        return new StringValue(str.Value.ToLowerInvariant());
                    }),
                "trim" => new FunctionValue(
                    "trim",
                    args =>
                    {
                        if (args.Count != 0) throw Error(member, "trim() 期望 0 个参数");
                        return new StringValue(str.Value.Trim());
                    }),
                "contains" => new FunctionValue(
                    "contains",
                    args =>
                    {
                        if (args.Count != 1) throw Error(member, "contains() 期望 1 个参数");
                        return BoolValue.Create(str.Value.Contains(args[0].As<StringValue>().Value));
                    }),
                "startsWith" => new FunctionValue(
                    "startsWith",
                    args =>
                    {
                        if (args.Count != 1) throw Error(member, "startsWith() 期望 1 个参数");
                        return BoolValue.Create(str.Value.StartsWith(args[0].As<StringValue>().Value));
                    }),
                "endsWith" => new FunctionValue(
                    "endsWith",
                    args =>
                    {
                        if (args.Count != 1) throw Error(member, "endsWith() 期望 1 个参数");
                        return BoolValue.Create(str.Value.EndsWith(args[0].As<StringValue>().Value));
                    }),
                _ => throw Error(member, $"未知的字符串方法 '{member.Property}'")
            };

            #endregion
        }



        // 处理 ClrObjectValue（CLR 对象）
        if (target is ClrObjectValue clrObj)
        {
            return AccessClrProperty(member, clrObj, member.Property);
        }

        throw Error(member, $"无法访问 {target.GetType()} 上的属性 '{member.Property}'");
    }

    /// <summary>
    /// 访问索引
    /// </summary>
    /// <param name="index"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    private async Task<Value> EvaluateIndexAccessAsync(IndexAccessExpr index, Scope scope)
    {
        Value target = (await EvaluateAsync(index.Target, scope)).Value;
        Value idx = (await EvaluateAsync(index.Index, scope)).Value;

        if (target is ArrayValue arr && idx.IsNumber_Int)
        {
            int i = idx.As<int>();
            if (i < 0 || i >= arr.Elements.Count)
                throw Error(index, $"数组索引越界: {i}");
            return arr.Get(i);
        }

        if (target is StringValue str && idx.IsNumber_Int)
        {
            int i = idx.As<int>();
            if (i < 0 || i >= str.Value.Length)
                throw Error(index, $"字符串索引越界: {i}");
            return new StringValue(str.Value[i].ToString());
        }

        if (target is ObjectValue obj && idx is StringValue key)
        {
            if (obj.TryGetValue(key.Value, out var value))
            {
                return value;
            }
            throw Error(index, $"对象中未找到键 '{key.Value}'");
        }

        throw Error(index, $"无效的索引访问，类型为 {target.GetType()}");
    }

    /// <summary>
    /// 索引赋值（arr[index] = value）
    /// </summary>
    private async Task<Value> EvaluateIndexAssignAsync(IndexAssignExpr indexAssign, Scope scope)
    {
        Value target = (await EvaluateAsync(indexAssign.Target, scope)).Value;
        Value idx = (await EvaluateAsync(indexAssign.Index, scope)).Value;
        Value value = (await EvaluateAsync(indexAssign.Value, scope)).Value;

        if (target is ArrayValue arr && idx.IsNumber_Int)
        {
            int i = (int)idx.As<int>();
            if (i < 0 || i >= arr.Elements.Count)
                throw Error(indexAssign, $"数组索引越界: {i}");
            //arr.Elements[i] = value;
            arr.Set(i, value, _engine);
            return value;
        }

        if (target is ObjectValue obj && idx is StringValue key)
        {
            obj.Set(key.Value, value);
            //obj.Properties[key.Value] = value;
            return value;
        }

        throw Error(indexAssign, $"无法对 {target.GetType()} 类型的索引进行赋值");
    }

    /// <summary>
    /// 设置成员
    /// </summary>
    /// <param name="memberAssign"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    private async Task<Value> EvaluateMemberAssignAsync(MemberAssignExpr memberAssign, Scope scope)
    {
        // 先计算目标对象
        Value target = (await EvaluateAsync(memberAssign.Target, scope)).Value;

        // 安全访问 ?. 如果目标为 null 且 SafeNull 为 true，直接返回 null
        if (target.IsNull && memberAssign.SafeNull)
        {
            return Value.Null;
        }

        var value = (await EvaluateAsync(memberAssign.Value, scope)).Value; ;

        // ObjectValue（脚本对象）属性赋值
        if (target is ObjectValue obj)
        {
            obj.Set(memberAssign.Property, value);
            //obj.Properties[memberAssign.Property] = value;
            return value;
        }

        // ClrObjectValue（CLR 对象）属性赋值
        if (target is ClrObjectValue clrObj)
        {
            SetClrProperty(memberAssign, clrObj, memberAssign.Property, value);
            //var memberAccessExpr = new MemberAccessExpr(memberAssign.Target, memberAssign.Property, memberAssign.SafeNull, null);
            return value;
        }

        throw Error(memberAssign, $"无法为 {target.GetType()} 上的属性 '{memberAssign.Property}' 赋值");
    }

    /// <summary>
    /// CLR对象成员缓存： (Type, Name) -> MemberInfo
    /// </summary>
    private readonly Dictionary<(Type, string), MemberInfo> _memberInfoCaches = new();

    /// <summary>
    /// 设置 CLR 对象属性（带缓存）
    /// </summary>
    private void SetClrProperty(MemberAssignExpr memberAssign, ClrObjectValue clrObj, string propertyName, Value value)
    {
        var type = clrObj.ClrObject!.GetType();
        var member = GetOrAddMemberInfo(type, propertyName, memberAssign);

        if (member is not PropertyInfo prop || !prop.CanWrite)
            throw Error(memberAssign, $"CLR 对象上未找到属性 '{propertyName}' 或该属性不可写");

        var newValue = ConvertScriptValueToClrValue(memberAssign, value, prop.PropertyType);
        prop.SetValue(clrObj.ClrObject, newValue);
    }

    /// <summary>
    /// 访问 CLR 对象属性 / 方法（带缓存）
    /// </summary>
    private Value AccessClrProperty(MemberAccessExpr member, ClrObjectValue clrObj, string propertyName)
    {
        var type = clrObj.ClrObject!.GetType();
        var memberInfo = GetOrAddMemberInfo(type, propertyName, member);

        try
        {
            switch (memberInfo)
            {
                case PropertyInfo prop:
                    var val = prop.GetValue(clrObj.ClrObject);
                    return ConvertClrValueToScriptValue(val);

                case MethodInfo method:
                    var methodValue = new ClrMethodValue(method);
                    if (!method.IsStatic)
                        methodValue = methodValue with { TargetInstance = clrObj.ClrObject };
                    return methodValue;

                default:
                    throw Error(member, $"CLR 类型 {type.Name} 不支持成员 '{propertyName}'");
            }
        }
        catch (Exception ex)
        {
            throw Error(member, $"访问成员 '{propertyName}' 时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 从缓存获取或反射解析 MemberInfo
    /// </summary>
    private MemberInfo GetOrAddMemberInfo(Type type, string name, Expr expr)
    {
        var key = (type, name);
        if (_memberInfoCaches.TryGetValue(key, out var cached))
            return cached;

        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

        // 先找属性，再找方法
        var member =
            (MemberInfo?)type.GetProperty(name, flags)
            ?? type.GetMethod(name, flags);

        if (member == null)
            throw Error(expr, $"CLR 类型 {type.Name} 上未找到属性或方法 '{name}'");

        _memberInfoCaches[key] = member;
        return member;
    }

    /// <summary>
    /// 将 CLR 值转换为脚本 Value
    /// </summary>
    private Value ConvertClrValueToScriptValue(object? clrValue)
    {
        if (clrValue is null || clrValue == System.DBNull.Value)
            return Value.Null;

        var type = clrValue?.GetType();
        if (type is null)
            return Value.Null;

        if (type == typeof(int))
            return NumberValue<int>.Create(Convert.ToInt32(clrValue));
        if (type == typeof(long))
            return NumberValue<long>.Create(Convert.ToInt64(clrValue));
        if (type == typeof(float))
            return NumberValue<float>.Create(Convert.ToSingle(clrValue));
        if (type == typeof(double))
            return NumberValue<double>.Create(Convert.ToDouble(clrValue));
        if (type == typeof(decimal))
            return NumberValue<decimal>.Create(Convert.ToDecimal(clrValue));

        if (type == typeof(bool))
            return BoolValue.Create(clrValue is bool);

        if (type == typeof(string) || type == typeof(char))
            return new StringValue(clrValue?.ToString() ?? string.Empty);

        // 集合类型
        if (clrValue is IEnumerable enumerable && clrValue is not string)
        {
            var elements = new List<Value>();
            foreach (var item in enumerable)
            {
                elements.Add(ConvertClrValueToScriptValue(item));
            }
            return new ArrayValue(elements);
        }

        // 字典类型
        if (clrValue is IDictionary dict)
        {
            var properties = new Dictionary<string, Value>();
            foreach (DictionaryEntry entry in dict)
            {
                var key = entry.Key?.ToString();
                if (key != null)
                {
                    var value = ConvertClrValueToScriptValue(entry.Value);
                    //var member = new MemberValue(key, vakue);
                    properties[key] = value;
                }
            }
            return new ObjectValue(properties);
        }
        
        // 其他 CLR 对象包装
        return new ClrObjectValue(clrValue);
    }

    /// <summary>
    /// 将脚本 Value 转换为 CLR 值
    /// </summary>
    private object? ConvertScriptValueToClrValue(Expr expr, Value value, Type targetType)
    {
        // 如果目标类型就是 Value，直接返回
        if (targetType.IsAssignableFrom(typeof(Value)))
            return value;

        // 回调
        if (targetType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }
        // 处理 Null
        if (value.IsNull)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                throw Error(expr, $"无法将 null 赋值给不可为 null 的值类型 {targetType.Name}");
            return null;
        }


        // 处理 ClrObjectValue（直接返回包装的对象）
        if (value is ClrObjectValue clrObj)
        {
            if (targetType.IsAssignableFrom(clrObj.ClrObject!.GetType()))
                return clrObj.ClrObject;
            throw Error(expr, $"无法将类型为 {clrObj.ClrObject.GetType().Name} 的 CLR 对象转换为 {targetType.Name}");
        }

        // 数值类型
        if (value.IsNumber)
        {
            //double num = value.AsNumber();
            if (targetType == typeof(int)) return value.As<int>();
            if (targetType == typeof(long)) return value.As<long>();
            if (targetType == typeof(float)) return value.As<float>();
            if (targetType == typeof(double)) return value.As<double>();
            if (targetType == typeof(decimal)) return value.As<decimal>();
            if (targetType == typeof(short)) return value.As<short>();
            if (targetType == typeof(byte)) return value.As<byte>();
            if (targetType == typeof(sbyte)) return value.As<sbyte>();
            if (targetType == typeof(uint)) return value.As<uint>();
            if (targetType == typeof(ulong)) return value.As<ulong>();
            if (targetType == typeof(ushort)) return value.As<ushort>();
        }

        // 字符串
        if (value.IsString && targetType == typeof(string))
            return value.AsString();

        // 布尔
        if (value.IsBool && targetType == typeof(bool))
            return value.AsBool();

        // 数组/集合
        if (value.IsArray)
        {
            var elementValues = value.As<ArrayValue>().Elements;

            // 目标类型是 CLR 数组
            if (targetType.IsArray)
            {
                var elementType = targetType.GetElementType() ?? typeof(object);
                var array = Array.CreateInstance(elementType, elementValues.Count);
                for (int i = 0; i < elementValues.Count; i++)
                {
                    var index = ConvertScriptValueToClrValue(expr, elementValues[i], elementType);
                    array.SetValue(index, i);
                }
                return array;
            }

            // 目标类型是 List<T>
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = targetType.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(targetType)!;
                foreach (var v in elementValues)
                {
                    var item = ConvertScriptValueToClrValue(expr, v, elementType);
                    list.Add(item);
                }
                return list;
            }

            // 目标类型是 IEnumerable 或可分配给 IEnumerable<object>
            if (typeof(IEnumerable).IsAssignableFrom(targetType))
            {
                return elementValues.Select(v => ConvertScriptValueToClrValue(expr, v, typeof(object))).ToArray();
            }

            // 原来的 object[] 兼容
            if (targetType.IsAssignableFrom(typeof(object[])))
            {
                return elementValues.Select(v => ConvertScriptValueToClrValue(expr, v, typeof(object))).ToArray();
            }
        }

        throw Error(expr, $"无法将类型为 {value.GetType()} 的脚本值转换为 CLR 类型 {targetType.Name}");
    }

    /// <summary>
    /// 在当前作用域获取成员
    /// </summary>
    /// <param name="identifier"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<Value> EvaluateIdentifierAsync(IdentifierExpr identifier, Scope scope)
    {
        var name = identifier.Name;
        if (scope.TryGetValue(name, out var info)
            || _engine.GlobalScope.TryGetValue(name, out info))
        {
            return info.Value;
        }
        throw new RuntimeException($"未定义的变量 '{name}'");
    }


    /// <summary>
    /// 调用 CLR 方法（自动 await）
    /// </summary>
    private async Task<Value> CallClrMethodAsync(CallExpr call, ClrMethodValue clrMethod, List<Value> args)
    {
        var method = clrMethod.Delegate.MethodInfo;
        var parameters = method.GetParameters();

        // 验证参数数量
        if (args.Count < parameters.Length)
        {
            int requiredParams = parameters.Count(p => !p.IsOptional);
            if (args.Count < requiredParams)
            {
                throw Error(call, $"CLR 方法期望至少 {requiredParams} 个参数，但实际收到 {args.Count} 个");
            }
        }
        else if (args.Count > parameters.Length)
        {
            var lastParam = parameters.LastOrDefault();
            if (lastParam == null || System.Attribute.GetCustomAttribute(lastParam, typeof(System.ParamArrayAttribute)) == null)
            {
                throw Error(call, $"CLR 方法期望 {parameters.Length} 个参数，但实际收到 {args.Count} 个");
            }
        }

        // 转换参数
        var clrArgs = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i < args.Count)
            {
                var tobj = args[i];
                var type = parameters[i].ParameterType;
                clrArgs[i] = ConvertScriptValueToClrValue(call, tobj, type);
            }
            else if (parameters[i].IsOptional)
            {
                clrArgs[i] = parameters[i].DefaultValue;
            }
        }

        try
        {
            var result = await clrMethod.Delegate.InvokeAsync(clrMethod.TargetInstance, clrArgs);
            return ConvertClrValueToScriptValue(result);
        }
        catch (System.Reflection.TargetInvocationException tie)
        {
            throw Error(call, $"CLR 方法调用失败: {tie.InnerException?.Message ?? tie.Message}");
        }
        catch (Exception ex)
        {
            throw Error(call, $"调用 CLR 方法时出错: {ex.Message}");
        }
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 
    /// </summary>
    /// <param name="expr"></param>
    /// <param name="message"></param>
    /// <returns></returns>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RuntimeException Error(Expr expr, string message)
    {
        var ss = expr.SourceSpan;
        var filePath = ss.FilePath;
        var slice = _engine.SourceManager.GetSlice(filePath, ss.Start, ss.Length);

        string tokenInfo = $"(类型: {expr.GetType()})";
        string fullMessage = $"{message}{tokenInfo} {Environment.NewLine}  `{slice}` {ss.ToString()}";
        return new RuntimeException(expr, fullMessage);
    }

    /// <summary>
    /// 判断值是否可用
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static bool IsTrue(Value value)
    {
        return value switch
        {
            NullValue => false,
            BoolValue b => b.Value,
            //NumberValue n => n.Value != 0,
            StringValue s => s.Value.Length > 0,
            ArrayValue a => a.Elements.Count > 0,
            ObjectValue o => o.Properties.Count > 0,
            _ => false,
        };
    }

    /// <summary>
    /// 判断值是否相等
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    private static bool IsEqual(Value left, Value right)
    {
        

        if (left.IsNumber && right.IsNumber)
        {
            if (left.IsNumber_Decimal|| right.IsNumber_Decimal)
            {
                return left.As<decimal>() == right.As<decimal>();
            }
            else if (left.IsNumber_Double || right.IsNumber_Double)
            {
                return Math.Abs(left.As<double>() - right.As<double>()) < double.Epsilon;
            }
            else if (left.IsNumber_Float || right.IsNumber_Float)
            {
                return Math.Abs(left.As<float>() - right.As<float>()) < double.Epsilon;
            }
            else if (left.IsNumber_Long || right.IsNumber_Long)
            {
                return left.As<long>() == right.As<long>();
            }
            else if (left.IsNumber_Int || right.IsNumber_Int)
            {
                return left.As<int>() == right.As<int>();
            }
        }

        /*if (left.GetType() != right.GetType())
            return false;*/

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
            _ => left.As<object>() == right.As<object>(),
        };
    }


}

/// <summary>
/// 运行时异常
/// </summary>
public class RuntimeException : Exception
{
    public RuntimeException(string message) : base(message) { }
    public RuntimeException(Expr expr, string message) : base(message) { }
    //public RuntimeException(string message) : base(message) { }
}
