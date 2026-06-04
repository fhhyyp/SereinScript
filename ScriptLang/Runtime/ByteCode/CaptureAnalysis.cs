using ScriptLang.Parser;
using System;
using System.Collections.Generic;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>
/// 闭包捕获分析器（编译时静态分析）
/// 分析 Lambda 表达式中的自由变量，确定哪些外部变量需要被闭包捕获
/// </summary>
public static class CaptureAnalysis
{
    /// <summary>
    /// 分析 Lambda 中的自由变量
    /// </summary>
    /// <param name="lambda">Lambda 表达式</param>
    /// <param name="localNames">当前作用域中已知的局部变量名集合</param>
    /// <returns>需要捕获的自由变量名集合</returns>
    public static HashSet<string> Analyze(LambdaExpr lambda, HashSet<string> localNames)
    {
        var boundVars = new HashSet<string>(lambda.Params);
        var freeVars = new HashSet<string>();

#if DEBUG
        Console.WriteLine($"[CaptureAnalysis] === 开始分析 Lambda ===");
        Console.WriteLine($"[CaptureAnalysis] Lambda 参数: [{string.Join(", ", lambda.Params)}]");
        Console.WriteLine($"[CaptureAnalysis] Lambda Body 类型: {lambda.Body.GetType().Name}");
        Console.WriteLine($"[CaptureAnalysis] localNames 内容 ({localNames.Count} 个): [{string.Join(", ", localNames)}]");
#endif

        CollectFreeVariables(lambda.Body, boundVars, freeVars);

#if DEBUG
        Console.WriteLine($"[CaptureAnalysis] 所有自由变量 ({freeVars.Count} 个): [{string.Join(", ", freeVars)}]");
#endif

        // 只保留在当前作用域中实际存在的局部变量（过滤全局变量、内置函数等）
        var captured = new HashSet<string>();
        foreach (var name in freeVars)
        {
            bool isLocal = localNames.Contains(name);
            bool isBound = boundVars.Contains(name);

#if DEBUG
            Console.WriteLine($"[CaptureAnalysis]   变量 '{name}': isLocal={isLocal}, isBound={isBound}");
#endif

            if (isLocal && !isBound)
            {
                captured.Add(name);
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]     → 捕获!");
#endif
            }
        }

#if DEBUG
        Console.WriteLine($"[CaptureAnalysis] 最终捕获 ({captured.Count} 个): [{string.Join(", ", captured)}]");
        Console.WriteLine($"[CaptureAnalysis] === 分析结束 ===\n");
#endif

        return captured;
    }

    private static void CollectFreeVariables(Expr expr, HashSet<string> bound, HashSet<string> free)
    {
        switch (expr)
        {
            case IdentifierExpr id:
                if (!bound.Contains(id.Name))
                {
#if DEBUG
                    Console.WriteLine($"[CaptureAnalysis]   CollectFree: 发现自由变量 '{id.Name}' (IdentifierExpr)");
#endif
                    free.Add(id.Name);
                }
                break;

            case LiteralExpr:
                break;

            case LetExpr let:
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]   CollectFree: 进入 LetExpr '{let.Name}'");
#endif
                CollectFreeVariables(let.Value, bound, free);
                break;

            case VarExpr var:
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]   CollectFree: 进入 VarExpr '{var.Name}'");
#endif
                CollectFreeVariables(var.Value, bound, free);
                break;

            case AssignExpr assign:
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]   CollectFree: 进入 AssignExpr '{assign.Name}'");
#endif
                CollectFreeVariables(assign.Value, bound, free);
                if (!bound.Contains(assign.Name))
                    free.Add(assign.Name);
                break;

            case BlockExpr block:
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]   CollectFree: 进入 BlockExpr ({block.Statements.Count} 条语句)");
#endif
                var blockBound = new HashSet<string>(bound);
                for (int idx = 0; idx < block.Statements.Count; idx++)
                {
                    var stmt = block.Statements[idx];
                    if (stmt is LetExpr l)
                    {
#if DEBUG
                        Console.WriteLine($"[CaptureAnalysis]     Block[{idx}]: LetExpr '{l.Name}' - 先求值, 再绑定");
#endif
                        CollectFreeVariables(l.Value, blockBound, free);
                        blockBound.Add(l.Name);
#if DEBUG
                        Console.WriteLine($"[CaptureAnalysis]     Block: 绑定 '{l.Name}' → bound 现在: [{string.Join(", ", blockBound)}]");
#endif
                    }
                    else if (stmt is VarExpr v)
                    {
#if DEBUG
                        Console.WriteLine($"[CaptureAnalysis]     Block[{idx}]: VarExpr '{v.Name}' - 先求值, 再绑定");
#endif
                        CollectFreeVariables(v.Value, blockBound, free);
                        blockBound.Add(v.Name);
#if DEBUG
                        Console.WriteLine($"[CaptureAnalysis]     Block: 绑定 '{v.Name}' → bound 现在: [{string.Join(", ", blockBound)}]");
#endif
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine($"[CaptureAnalysis]     Block[{idx}]: {stmt.GetType().Name}");
#endif
                        CollectFreeVariables(stmt, blockBound, free);
                    }
                }
                break;

            case LambdaExpr lambda:
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]   CollectFree: 进入内层 Lambda, 参数: [{string.Join(", ", lambda.Params)}]");
#endif
                var lambdaBound = new HashSet<string>(bound);
                lambdaBound.UnionWith(lambda.Params);
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]     内层 Lambda bound: [{string.Join(", ", lambdaBound)}]");
#endif
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
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]   CollectFree: 进入 CallExpr (target: {call.Target.GetType().Name}, args: {call.Args.Count})");
#endif
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
                if (whenExpr.OtherClause != null)
                    CollectFreeVariables(whenExpr.OtherClause.Body, bound, free);
                break;

            case ForExpr forExpr:
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]   CollectFree: 进入 ForExpr '{forExpr.VarName}'");
#endif
                CollectFreeVariables(forExpr.Iterable, bound, free);
                var forBound = new HashSet<string>(bound) { forExpr.VarName };
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]     For: 绑定循环变量 '{forExpr.VarName}' → bound: [{string.Join(", ", forBound)}]");
#endif
                CollectFreeVariables(forExpr.Body, forBound, free);
                break;

            case ReturnExpr ret:
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]   CollectFree: 进入 ReturnExpr (HasValue={ret.Value != null})");
                if (ret.Value != null)
                    Console.WriteLine($"[CaptureAnalysis]     Return 值类型: {ret.Value.GetType().Name}");
#endif
                if (ret.Value != null)
                    CollectFreeVariables(ret.Value, bound, free);
                break;

            case ArrayLiteralExpr arr:
                foreach (var elem in arr.Elements)
                    CollectFreeVariables(elem, bound, free);
                break;

            case ObjectLiteralExpr obj:
                foreach (var prop in obj.Properties)
                    CollectFreeVariables(prop.Value, bound, free);
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
#if DEBUG
                Console.WriteLine($"[CaptureAnalysis]   CollectFree: 进入 ProgramExpr ({program.Statements.Count} 条语句)");
#endif
                var programBound = new HashSet<string>(bound);
                for (int idx = 0; idx < program.Statements.Count; idx++)
                {
                    var stmt = program.Statements[idx];
                    if (stmt is LetExpr l2)
                    {
#if DEBUG
                        Console.WriteLine($"[CaptureAnalysis]     Program[{idx}]: LetExpr '{l2.Name}'");
#endif
                        CollectFreeVariables(l2.Value, programBound, free);
                        programBound.Add(l2.Name);
                    }
                    else if (stmt is VarExpr v2)
                    {
#if DEBUG
                        Console.WriteLine($"[CaptureAnalysis]     Program[{idx}]: VarExpr '{v2.Name}'");
#endif
                        CollectFreeVariables(v2.Value, programBound, free);
                        programBound.Add(v2.Name);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine($"[CaptureAnalysis]     Program[{idx}]: {stmt.GetType().Name}");
#endif
                        CollectFreeVariables(stmt, programBound, free);
                    }
                }
                break;

            case ErrorExpr:
            case ImportStmt:
                break;
        }
    }
}