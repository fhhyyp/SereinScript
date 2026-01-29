using ScriptLang.Lexer;
using ScriptLang.Parser;
using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
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

    /*/// <summary>
    /// 构造函数
    /// </summary>
    public Interpreter(string? baseDirectory = null)
    {
        _importResolver = new ImportResolver(this, baseDirectory);
    }*/

    /// <summary>
    /// 异步执行主入口
    /// </summary>
    public async Task<EvalResult> EvaluateAsync(Expr expr, Scope scope)
    {
        if (expr is Parser.Program program)
            return await EvaluateProgramAsync(program, scope);

        var value = expr switch
        {
            ReturnExpr returnExpr => await EvaluateReturnAsync(returnExpr, scope),
            ImportStmt import => (await EvaluateImportAsync(import, scope)).FormResult(),
            LiteralExpr literal => (EvaluateLiteral(literal)).FormResult(),
            IdentifierExpr identifier => (await EvaluateIdentifierAsync(identifier, scope)).FormResult(),
            LetExpr let => (await EvaluateLetAsync(let, scope)).FormResult(),
            VarExpr var => (await EvaluateVarAsync(var, scope)).FormResult(),
            AssignExpr assign => (await EvaluateAssignAsync(assign, scope)).FormResult(),
            IndexAssignExpr indexAssign => (await EvaluateIndexAssignAsync(indexAssign, scope)).FormResult(),
            BinaryExpr binary => (await EvaluateBinaryAsync(binary, scope)).FormResult(),
            UnaryExpr unary => (await EvaluateUnaryAsync(unary, scope)).FormResult(),
            ConditionalExpr conditional => (await EvaluateConditional(conditional, scope)).FormResult(),
            IfExpr ifExpr => (await EvaluateIfAsync(ifExpr, scope)).FormResult(),
            WhenExpr whenExpr => (await EvaluateWhenAsync(whenExpr, scope)).FormResult(),
            ForExpr forExpr => (await EvaluateForAsync(forExpr, scope)).FormResult(),
            LambdaExpr lambda => (EvaluateLambda(lambda, scope)).FormResult(),
            CallExpr call => (await EvaluateCallAsync(call, scope)).FormResult(),
            BlockExpr block => (await EvaluateBlockAsync(block, scope)).FormResult(),
            ArrayLiteralExpr array => (await EvaluateArrayAsync(array, scope)).FormResult(),
            ObjectLiteralExpr obj => (await EvaluateObjectAsync(obj, scope)).FormResult(),
            MemberAccessExpr member => (await EvaluateMemberAccessAsync(member, scope)).FormResult(),
            MemberAssignExpr memberAssign => (await EvaluateMemberAssignAsync(memberAssign, scope)).FormResult(),
            IndexAccessExpr index => (await EvaluateIndexAccessAsync(index, scope)).FormResult(),
            _ => throw Error(expr, $"Unknown expression type")
        };
        return value;
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
            double d => new NumberValue(d),
            int i => new NumberValue(i),
            string s => new StringValue(s),
            bool b => new BoolValue(b),
            _ => throw Error(literal, $"Unsupported literal type: {literal.Value?.GetType()}")
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
                return new NumberValue(left.AsNumber() + right.AsNumber());
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
                return new NumberValue(left.AsNumber() - right.AsNumber());
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
                return new NumberValue(left.AsNumber() * right.AsNumber());
            if (left.IsString && right.IsNumber)
            {
                var str = left.AsString();
                var repeat = (int)right.AsNumber();
                return new StringValue(string.Concat(Enumerable.Repeat(str, repeat)));
            }
        }

        if (binary.Op == "/")
        {
            if (left.IsNumber && right.IsNumber)
                return new NumberValue(left.AsNumber() / right.AsNumber());
        }

        if (binary.Op == "%")
        {
            if (left.IsNumber && right.IsNumber)
                return new NumberValue(left.AsNumber() % right.AsNumber());
        }

        // 比较运算
        if (binary.Op == "==")
            return new BoolValue(IsEqual(left, right));

        if (binary.Op == "!=")
            return new BoolValue(!IsEqual(left, right));

        if (binary.Op == "<")
        {
            if (left.IsNumber && right.IsNumber)
                return new BoolValue(left.AsNumber() < right.AsNumber());
            if (left.IsString && right.IsString)
                return new BoolValue(string.Compare(left.AsString(), right.AsString()) < 0);
        }

        if (binary.Op == "<=")
        {
            if (left.IsNumber && right.IsNumber)
                return new BoolValue(left.AsNumber() <= right.AsNumber());
            if (left.IsString && right.IsString)
                return new BoolValue(string.Compare(left.AsString(), right.AsString()) <= 0);
        }

        if (binary.Op == ">")
        {
            if (left.IsNumber && right.IsNumber)
                return new BoolValue(left.AsNumber() > right.AsNumber());
            if (left.IsString && right.IsString)
                return new BoolValue(string.Compare(left.AsString(), right.AsString()) > 0);
        }

        if (binary.Op == ">=")
        {
            if (left.IsNumber && right.IsNumber)
                return new BoolValue(left.AsNumber() >= right.AsNumber());
            if (left.IsString && right.IsString)
                return new BoolValue(string.Compare(left.AsString(), right.AsString()) >= 0);
        }

        // 逻辑运算
        if (binary.Op == "&&")
            return new BoolValue(IsTruthy(left) && IsTruthy(right));

        if (binary.Op == "||")
            return new BoolValue(IsTruthy(left) || IsTruthy(right));

        throw Error(binary, $"Unsupported operator '{binary.Op}' for types {left.GetType()} and {right.GetType()}");
    }

    private async Task<Value> EvaluateUnaryAsync(UnaryExpr unary, Scope scope)
    {
        Value operand = (await EvaluateAsync(unary.Expr, scope)).Value;

        if (unary.Op == "-")
        {
            if (operand.IsNumber)
                return new NumberValue(-operand.AsNumber());
        }

        if (unary.Op == "!")
        {
            return new BoolValue(!IsTruthy(operand));
        }

        throw Error(unary, $"Unsupported unary operator '{unary.Op}' for type {operand.GetType()}");
    }

    private async Task<Value> EvaluateConditional(ConditionalExpr conditional, Scope scope)
    {
        Value cond = (await EvaluateAsync(conditional.Cond, scope)).Value;
        return IsTruthy(cond) ? (await EvaluateAsync(conditional.Then, scope)).Value : (await EvaluateAsync(conditional.Else, scope)).Value;
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
        return IsTruthy(cond) ? (await EvaluateAsync(ifExpr.Then, scope)).Value: (await EvaluateAsync(ifExpr.Else, scope)).Value;
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
                    if (i == 0)
                    {
                        loopScope.Define(forExpr.VarName, element);
                    }
                    else
                    {
                        loopScope.Set(forExpr.VarName, element);
                    }
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
            throw Error(forExpr, "For loop expects array");
        }

        return result;
    }

    // ==================== 函数 ====================

    private Value EvaluateLambda(LambdaExpr lambda, Scope scope)
    {
        return new FunctionValue(lambda.Params, lambda.Body, scope);
    }

    private async Task<Value> EvaluateCallAsync(CallExpr call, Scope scope)
    {
        Value target = (await EvaluateAsync(call.Target, scope)).Value;

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
        
        throw Error(call, $"Can only call functions, got {target.GetType()}");
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
        var properties = new Dictionary<string, Value>();
        foreach (var prop in obj.Properties)
        {
            var key = prop.Key;
            var value = (await EvaluateAsync(prop.Value, scope)).Value;
            if(value.Source is not null)
            {
                value.Source = value.Source;
                value.TargetKey = value.TargetKey;
                value.TargetIndex = value.TargetIndex;
            }
            properties[prop.Key] = value;
        }
        var objValue = new ObjectValue(properties);
        /*var temparr = objValue.Properties.Select(x => x.Value).Where(x => x.Value.Source is null).ToList();
        foreach (var item in temparr)
        {
            item.Value.TargetKey = item.TargetKey;
            item.Value.Source = objValue ;
        }*/
        return objValue;
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
                        throw Error(member, "has() expects 1 argument");

                    if (args[0] is not StringValue stringValue)
                        throw Error(member, "has() expects string");

                    var isContainsKey = obj.Properties.ContainsKey(stringValue.AsString());
                    return new BoolValue(isContainsKey);
                }),
                _ => throw Error(member, $"Property '{member.Property}' not found on object"),
            };
            #endregion
        }

        // 处理 ArrayValue（数组）
        if (target is ArrayValue arr)
        {
            #region 原生数组方法
            return member.Property switch
            {
                "length" => new NumberValue(arr.Elements.Count),
                "onNext" => new FunctionValue("onNext", async args =>
                    {
                        if (args.Count != 1) throw Error(member, "map() expects 1 argument");
                        if (args[0] is not FunctionValue func) throw Error(member, "map() expects a function");

                        var value = await func.CallAsync(_engine, arr);
                        value.Source = arr;
                        value.TargetKey = "*";
                        return value;
                    }),
                "map" => new FunctionValue(
                    "map",
                    async args =>
                    {
                        if (args.Count != 1) throw Error(member, "map() expects 1 argument");
                        if (args[0] is not FunctionValue func) throw Error(member, "map() expects a function");

                        var result = new List<Value>();
                        foreach (var item in arr.Elements)
                        {
                            // 调用函数
                            var callResult = await func.CallAsync(_engine, item);
                            result.Add(callResult);
                        }
                        return new ArrayValue(result);
                    }),
                "filter" => new FunctionValue(
                    "filter",
                    async args =>
                    {
                        if (args.Count != 1) throw Error(member, "filter() expects 1 argument");
                        if (args[0] is not FunctionValue func) throw Error(member, "filter() expects a function");

                        var result = new List<Value>();
                        foreach (var item in arr.Elements)
                        {
                            var callResult = await func.CallAsync(_engine, item);
                            if (IsTruthy(callResult))
                                result.Add(item);
                        }
                        return new ArrayValue(result);
                    }),
                "forEach" => new FunctionValue(
                    "forEach",
                    async args =>
                    {
                        if (args.Count != 1) throw Error(member, "forEach() expects 1 argument");
                        if (args[0] is not FunctionValue func) throw Error(member, "forEach() expects a function");

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
                        int start = (int)args[0].AsNumber();
                        int end = args.Count > 1 ? (int)args[1].AsNumber() : arr.Elements.Count;
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
                        throw Error(member, "remove() expects 1 argument");
                    var state = arr.Remove(args[0], _engine);
                    return new BoolValue(state);
                }),
                "removeAt" => new FunctionValue("removeAt", args =>
                {
                    if (args.Count != 1 || !args[0].IsNumber)
                        throw Error(member, "removeAt() expects a number");

                    var index = (int)args[0].AsNumber();
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
                        throw Error(member, "find() expects 1 argument");

                    if (args[0] is not FunctionValue func)
                        throw Error(member, "find() expects a function as argument");

                    foreach (var item in arr.Elements)
                    {
                        var result = await func.CallAsync(_engine, item);
                        if (IsTruthy(result))
                            return item;
                    }

                    return Value.Null; // 没有匹配的返回 null
                }),
                "findIndex" => new FunctionValue("findIndex", async args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "findIndex() expects 1 argument");

                    if (args[0] is not FunctionValue func)
                        throw Error(member, "findIndex() expects a function as argument");

                    for (int i = 0; i < arr.Elements.Count; i++)
                    {
                        var result = await func.CallAsync(_engine, arr.Elements[i]);
                        if (IsTruthy(result))
                            return new NumberValue(i);
                    }

                    return new NumberValue(-1);
                }),

                // some
                "some" => new FunctionValue("some", async args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "some() expects 1 argument");

                    if (args[0] is not FunctionValue func)
                        throw Error(member, "some() expects a function as argument");

                    foreach (var item in arr.Elements)
                    {
                        var result = await func.CallAsync(_engine, item);
                        if (IsTruthy(result))
                            return new BoolValue(true);
                    }

                    return new BoolValue(false);
                }),

                // every
                "every" => new FunctionValue("every", async args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "every() expects 1 argument");

                    if (args[0] is not FunctionValue func)
                        throw Error(member, "every() expects a function as argument");

                    foreach (var item in arr.Elements)
                    {
                        var result = await func.CallAsync(_engine, item);
                        if (!IsTruthy(result))
                            return new BoolValue(false);
                    }

                    return new BoolValue(true);
                }),

                // every
                "join" => new FunctionValue("join", args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "join() expects 1 argument");

                    if (args[0] is not StringValue stringValue)
                        throw Error(member, "join() expects a function as argument");

                    var joinResult = string.Join(stringValue.AsString(), arr.Elements.Select(x => x.AsString()));
                    return new StringValue(joinResult);
                }),
                "onChanged" => new FunctionValue("onChanged", args =>
                {
                    if (args.Count != 1)
                        throw Error(member, "onChanged() expects 1 argument");

                    if (args[0] is not FunctionValue functionValue)
                        throw Error(member, "onChanged() expects function");

                    arr.AddOnChanged(functionValue);

                    //var isContainsKey = obj.Properties.ContainsKey(stringValue.AsString());
                    return Value.Null;
                }),
                _ => throw Error(member, $"Unknown array method '{member.Property}'")
            };
            #endregion
        }

        // 处理 StringValue（字符串）
        if (target is StringValue str)
        {

            #region 原生方法列表
            return member.Property switch
            {
                "length" => new NumberValue(str.Value.Length),
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
                        if (args.Count != 1) throw Error(member, "split() expects 1 argument");
                        var sep = args[0].As<StringValue>().Value;
                        var parts = str.Value.Split(sep);
                        return new ArrayValue(parts.Select(s => (Value)new StringValue(s)).ToList());
                    }),
                "substring" => new FunctionValue(
                    "substring",
                    args =>
                    {
                        if (args.Count < 1 || args.Count > 2)
                            throw Error(member, "substring() expects 1 or 2 arguments");
                        int start = (int)args[0].AsNumber();
                        int length = args.Count == 2 ? (int)args[1].AsNumber() : str.Value.Length - start;
                        return new StringValue(str.Value.Substring(start, length));
                    }),
                "toUpperCase" => new FunctionValue(
                    "toUpperCase",
                    args =>
                    {
                        if (args.Count != 0) throw Error(member, "toUpperCase() expects no arguments");
                        return new StringValue(str.Value.ToUpperInvariant());
                    }),
                "toLowerCase" => new FunctionValue(
                    "toLowerCase",
                    args =>
                    {
                        if (args.Count != 0) throw Error(member, "toLowerCase() expects no arguments");
                        return new StringValue(str.Value.ToLowerInvariant());
                    }),
                "trim" => new FunctionValue(
                    "trim",
                    args =>
                    {
                        if (args.Count != 0) throw Error(member, "trim() expects no arguments");
                        return new StringValue(str.Value.Trim());
                    }),
                "contains" => new FunctionValue(
                    "contains",
                    args =>
                    {
                        if (args.Count != 1) throw Error(member, "contains() expects 1 argument");
                        return new BoolValue(str.Value.Contains(args[0].As<StringValue>().Value));
                    }),
                "startsWith" => new FunctionValue(
                    "startsWith",
                    args =>
                    {
                        if (args.Count != 1) throw Error(member, "startsWith() expects 1 argument");
                        return new BoolValue(str.Value.StartsWith(args[0].As<StringValue>().Value));
                    }),
                "endsWith" => new FunctionValue(
                    "endsWith",
                    args =>
                    {
                        if (args.Count != 1) throw Error(member, "endsWith() expects 1 argument");
                        return new BoolValue(str.Value.EndsWith(args[0].As<StringValue>().Value));
                    }),
                _ => throw Error(member, $"Unknown string method '{member.Property}'")
            };

            #endregion
        }


        // 处理 ClrObjectValue（CLR 对象）
        if (target is ClrObjectValue clrObj)
        {
            return AccessClrProperty(member, clrObj, member.Property);
        }

        throw Error(member, $"Cannot access property '{member.Property}' on {target.GetType()}");
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

        throw Error(memberAssign, $"Cannot assign property '{memberAssign.Property}' on {target.GetType()}");
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

        if (target is ArrayValue arr && idx.IsNumber)
        {
            int i = (int)idx.AsNumber();
            if (i < 0 || i >= arr.Elements.Count)
                throw Error(index, $"Array index out of bounds: {i}");
            return arr.Get(i);
        }

        if (target is StringValue str && idx.IsNumber)
        {
            int i = (int)idx.AsNumber();
            if (i < 0 || i >= str.Value.Length)
                throw Error(index, $"String index out of bounds: {i}");
            return new StringValue(str.Value[i].ToString());
        }

        if (target is ObjectValue obj && idx is StringValue key)
        {
            if (obj.TryGetValue(key.Value, out var value))
            {
                return value;
            }
            throw Error(index, $"Key '{key.Value}' not found in object");
        }

        throw Error(index, $"Invalid index access on {target.GetType()}");
    }

    /// <summary>
    /// 索引赋值（arr[index] = value）
    /// </summary>
    private async Task<Value> EvaluateIndexAssignAsync(IndexAssignExpr indexAssign, Scope scope)
    {
        Value target = (await EvaluateAsync(indexAssign.Target, scope)).Value;
        Value idx = (await EvaluateAsync(indexAssign.Index, scope)).Value;
        Value value = (await EvaluateAsync(indexAssign.Value, scope)).Value;

        if (target is ArrayValue arr && idx.IsNumber)
        {
            int i = (int)idx.AsNumber();
            if (i < 0 || i >= arr.Elements.Count)
                throw Error(indexAssign, $"Array index out of bounds: {i}");
            arr.Elements[i] = value;
            return value;
        }

        if (target is ObjectValue obj && idx is StringValue key)
        { 
            obj.Set(key.Value, value);
            //obj.Properties[key.Value] = value;
            return value;
        }

        throw Error(indexAssign, $"Cannot assign to index on {target.GetType()}");
    }

    // ==================== 辅助方法 ====================

    private static bool IsTruthy(Value value)
    {
        return value switch
        {
            NullValue => false,
            BoolValue b => b.Value,
            NumberValue n => n.Value != 0,
            StringValue s => s.Value.Length > 0,
            ArrayValue a => a.Elements.Count > 0,
            ObjectValue o => o.Properties.Count > 0,
            _ => true
        };
    }

    private static bool IsEqual(Value left, Value right)
    {
        if (left.GetType() != right.GetType())
            return false;

        return (left, right) switch
        {
            (NullValue, NullValue) => true,
            (NumberValue nl, NumberValue nr) => Math.Abs(nl.Value - nr.Value) < double.Epsilon,
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

    /// <summary>
    /// CLR对象成员缓存： (Type, Name) -> MemberInfo
    /// </summary>
    private readonly Dictionary<(Type, string), MemberInfo> _memberInfoCaches = new();

    /// <summary>
    /// 设置 CLR 对象属性（带缓存）
    /// </summary>
    private void SetClrProperty(MemberAssignExpr memberAssign, ClrObjectValue clrObj, string propertyName, Value value)
    {
        var type = clrObj.Target!.GetType();
        var member = GetOrAddMemberInfo(type, propertyName, memberAssign);

        if (member is not PropertyInfo prop || !prop.CanWrite)
            throw Error(memberAssign, $"Property '{propertyName}' not found or not writable on CLR object");

        var newValue = ConvertScriptValueToClrValue(memberAssign, value, prop.PropertyType);
        prop.SetValue(clrObj.Target, newValue);
    }

    /// <summary>
    /// 访问 CLR 对象属性 / 方法（带缓存）
    /// </summary>
    private Value AccessClrProperty(MemberAccessExpr member, ClrObjectValue clrObj, string propertyName)
    {
        var type = clrObj.Target!.GetType();
        var memberInfo = GetOrAddMemberInfo(type, propertyName, member);

        try
        {
            switch (memberInfo)
            {
                case PropertyInfo prop:
                    var val = prop.GetValue(clrObj.Target);
                    return ConvertClrValueToScriptValue(val);

                case MethodInfo method:
                    var methodValue = new ClrMethodValue(method);
                    if (!method.IsStatic)
                        methodValue = methodValue with { TargetInstance = clrObj.Target };
                    return methodValue;

                default:
                    throw Error(member, $"Member '{propertyName}' not supported on CLR type {type.Name}");
            }
        }
        catch (Exception ex)
        {
            throw Error(member, $"Error accessing member '{propertyName}': {ex.Message}");
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
            throw Error(expr, $"Property or method '{name}' not found on CLR type {type.Name}");

        _memberInfoCaches[key] = member;
        return member;
    }

    /*// ==================== CLR 互操作 ====================
    private readonly Dictionary<(Type, string), MemberInfo> _memberInfoCaches = [];

    /// <summary>
    /// 设置CLR对象
    /// </summary>
    private void SetClrProperty(MemberAssignExpr memberAssign, ClrObjectValue clrObj, string propertyName, Value value)
    {
        var type = clrObj.Target.GetType();
        var key = (type, propertyName);
        if (!_memberInfoCaches.TryGetValue(key, out var memberInfo)
            || memberInfo is not PropertyInfo prop)
        {
            var t_prop = clrObj.Target.GetType().GetProperty(propertyName);
            if (t_prop == null || !t_prop.CanWrite)
                throw Error(memberAssign, $"Property '{propertyName}' not found or not writable on CLR object");
            prop = t_prop;
        }
        var newValue = ConvertScriptValueToClrValue(memberAssign, value, prop.PropertyType);
        prop.SetValue(clrObj.Target, newValue);
    }
    /// <summary>
    /// 访问 CLR 对象的属性
    /// </summary>
    private Value AccessClrProperty(MemberAccessExpr member, ClrObjectValue clrObj, string propertyName)
    {
        var targetType = clrObj.Target.GetType();
        var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;

        var property = targetType.GetProperty(propertyName, bindingFlags);

        if (property == null)
        {
            // 尝试查找方法（方法可以作为值返回）
            var method = targetType.GetMethod(propertyName, bindingFlags);
            if (method != null)
            {
                var methodValue = new ClrMethodValue(method);
                if (!method.IsStatic)
                {
                    methodValue = methodValue with { TargetInstance = clrObj.Target };
                }
                return methodValue;
            }
            throw Error(member, $"Property or method '{propertyName}' not found on CLR type {targetType.Name}");
        }

        try
        {
            var value = property.GetValue(clrObj.Target);
            return ConvertClrValueToScriptValue(value);
        }
        catch (Exception ex)
        {
            throw Error(member, $"Error accessing property '{propertyName}': {ex.Message}");
        }
    }
*/

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

        // 基本类型
        if (type == typeof(int) || type == typeof(short) || type == typeof(byte) || type == typeof(long))
            return new NumberValue(System.Convert.ToDouble(clrValue));
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return new NumberValue(System.Convert.ToDouble(clrValue));
        if (type == typeof(bool))
            return new BoolValue(clrValue is bool);
        if (type == typeof(string) || type == typeof(char))
            return new StringValue(clrValue?.ToString() ?? string.Empty);

        // 集合类型
        if (clrValue is System.Collections.IEnumerable enumerable && clrValue is not string)
        {
            var elements = new List<Value>();
            foreach (var item in enumerable)
            {
                elements.Add(ConvertClrValueToScriptValue(item));
            }
            return new ArrayValue(elements);
        }

        // 字典类型
        if (clrValue is System.Collections.IDictionary dict)
        {
            var properties = new Dictionary<string, Value>();
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                var key = entry.Key?.ToString();
                if (key != null)
                {
                    var vakue = ConvertClrValueToScriptValue(entry.Value);
                    //var member = new MemberValue(key, vakue);
                    properties[key] = vakue;
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
                throw Error(expr, $"Cannot assign null to non-nullable value type {targetType.Name}");
            return null;
        }


        // 处理 ClrObjectValue（直接返回包装的对象）
        if (value is ClrObjectValue clrObj)
        {
            if (targetType.IsAssignableFrom(clrObj.Target!.GetType()))
                return clrObj.Target;
            throw Error(expr, $"CLR object of type {clrObj.Target.GetType().Name} cannot be cast to {targetType.Name}");
        }

        // 数值类型
        if (value.IsNumber)
        {
            double num = value.AsNumber();
            if (targetType == typeof(int)) return (int)num;
            if (targetType == typeof(long)) return (long)num;
            if (targetType == typeof(float)) return (float)num;
            if (targetType == typeof(double)) return num;
            if (targetType == typeof(decimal)) return (decimal)num;
            if (targetType == typeof(short)) return (short)num;
            if (targetType == typeof(byte)) return (byte)num;
            if (targetType == typeof(sbyte)) return (sbyte)num;
            if (targetType == typeof(uint)) return (uint)num;
            if (targetType == typeof(ulong)) return (ulong)num;
            if (targetType == typeof(ushort)) return (ushort)num;
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

        throw Error(expr, $"Cannot convert script value of type {value.GetType()} to CLR type {targetType.Name}");
    }


    private async Task<Value> EvaluateIdentifierAsync(IdentifierExpr identifier, Scope scope)
    {
        return scope.Get(identifier.Name);
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
                throw Error(call, $"CLR method expects at least {requiredParams} arguments, but got {args.Count}");
            }
        }
        else if (args.Count > parameters.Length)
        {
            var lastParam = parameters.LastOrDefault();
            if (lastParam == null || System.Attribute.GetCustomAttribute(lastParam, typeof(System.ParamArrayAttribute)) == null)
            {
                throw Error(call, $"CLR method expects {parameters.Length} arguments, but got {args.Count}");
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
            throw Error(call, $"CLR method invocation failed: {tie.InnerException?.Message ?? tie.Message}");
        }
        catch (Exception ex)
        {
            throw Error(call, $"Failed to invoke CLR method: {ex.Message}");
        }
    }

    private RuntimeException Error(Expr expr, string message)
    {
        var ss = expr.SourceSpan;
        var filePath = ss.FilePath;
        var slice = _engine.SourceManager.GetSlice(filePath, ss.Start, ss.Length);
        
        string tokenInfo = $"(type: {expr.GetType()})";
        string fullMessage = $"{message}{tokenInfo} {Environment.NewLine}  `{slice}` {ss.ToString()}";
        return new RuntimeException(expr, fullMessage);
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
