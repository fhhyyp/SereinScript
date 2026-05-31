using ScriptLang.Parser;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptLang.Runtime
{
    /// <summary>
    /// 闭包变量分析器：分析Lambda中实际使用的外部变量
    /// </summary>
    public static class ClosureAnalyzer
    {
        /// <summary>
        /// 分析Lambda表达式中的自由变量（外部变量）
        /// </summary>
        public static HashSet<string> AnalyzeFreeVariables(LambdaExpr lambda, Scope scope)
        {
            var freeVariables = new HashSet<string>();
            var boundVariables = new HashSet<string>(lambda.Params); // Lambda参数是绑定变量

            // 递归收集自由变量
            CollectFreeVariables(lambda.Body, boundVariables, freeVariables);

            // 只保留在作用域中实际存在的变量
            var capturedVariables = new HashSet<string>();
            foreach (var varName in freeVariables)
            {
                if (scope.Exists(varName) && !boundVariables.Contains(varName))
                {
                    capturedVariables.Add(varName);
                }
            }

            return capturedVariables;
        }

        private static void CollectFreeVariables(Expr expr, HashSet<string> bound, HashSet<string> free)
        {
            switch (expr)
            {
                case IdentifierExpr identifier:
                    // 标识符：如果不在绑定变量中，就是自由变量
                    if (!bound.Contains(identifier.Name))
                    {
                        free.Add(identifier.Name);
                    }
                    break;

                case LiteralExpr:
                    // 字面量：无变量
                    break;

                case LetExpr letExpr:
                    // let绑定：先分析初始值，再分析后续
                    CollectFreeVariables(letExpr.Value, bound, free);
                    var letBound = new HashSet<string>(bound) { letExpr.Name };
                    // 注意：let的作用域不包含自身初始值，但后续代码可以看到
                    break;

                case VarExpr varExpr:
                    // var绑定：同let
                    CollectFreeVariables(varExpr.Value, bound, free);
                    var varBound = new HashSet<string>(bound) { varExpr.Name };
                    break;

                case AssignExpr assignExpr:
                    CollectFreeVariables(assignExpr.Value, bound, free);
                    free.Add(assignExpr.Name); // 赋值目标也是自由变量
                    break;

                case BinaryExpr binaryExpr:
                    CollectFreeVariables(binaryExpr.Left, bound, free);
                    CollectFreeVariables(binaryExpr.Right, bound, free);
                    break;

                case UnaryExpr unaryExpr:
                    CollectFreeVariables(unaryExpr.Expr, bound, free);
                    break;

                case ConditionalExpr conditionalExpr:
                    CollectFreeVariables(conditionalExpr.Cond, bound, free);
                    CollectFreeVariables(conditionalExpr.Then, bound, free);
                    CollectFreeVariables(conditionalExpr.Else, bound, free);
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
                    // for循环变量会遮蔽外部变量
                    var forBound = new HashSet<string>(bound) { forExpr.VarName };
                    CollectFreeVariables(forExpr.Body, forBound, free);
                    break;

                case LambdaExpr lambdaExpr:
                    // 内部Lambda：有自己的参数作用域
                    var lambdaBound = new HashSet<string>(bound);
                    lambdaBound.UnionWith(lambdaExpr.Params);
                    CollectFreeVariables(lambdaExpr.Body, lambdaBound, free);
                    break;

                case CallExpr callExpr:
                    CollectFreeVariables(callExpr.Target, bound, free);
                    foreach (var arg in callExpr.Args)
                    {
                        CollectFreeVariables(arg, bound, free);
                    }
                    break;

                case BlockExpr blockExpr:
                    // 代码块：按顺序分析，let/var会扩展绑定变量
                    var blockBound = new HashSet<string>(bound);
                    foreach (var stmt in blockExpr.Statements)
                    {
                        CollectFreeVariablesWithScope(stmt, blockBound, free);
                    }
                    break;

                case ReturnExpr returnExpr:
                    if (returnExpr.Value != null)
                    {
                        CollectFreeVariables(returnExpr.Value, bound, free);
                    }
                    break;

                case ArrayLiteralExpr arrayExpr:
                    foreach (var element in arrayExpr.Elements)
                    {
                        CollectFreeVariables(element, bound, free);
                    }
                    break;

                case ObjectLiteralExpr objectExpr:
                    foreach (var prop in objectExpr.Properties)
                    {
                        CollectFreeVariables(prop.Value, bound, free);
                    }
                    break;

                case MemberAccessExpr memberExpr:
                    CollectFreeVariables(memberExpr.Target, bound, free);
                    break;

                case MemberAssignExpr memberAssignExpr:
                    CollectFreeVariables(memberAssignExpr.Target, bound, free);
                    CollectFreeVariables(memberAssignExpr.Value, bound, free);
                    break;

                case IndexAccessExpr indexExpr:
                    CollectFreeVariables(indexExpr.Target, bound, free);
                    CollectFreeVariables(indexExpr.Index, bound, free);
                    break;

                case IndexAssignExpr indexAssignExpr:
                    CollectFreeVariables(indexAssignExpr.Target, bound, free);
                    CollectFreeVariables(indexAssignExpr.Index, bound, free);
                    CollectFreeVariables(indexAssignExpr.Value, bound, free);
                    break;

                case ErrorExpr:
                    break;

                default:
                    // 未知表达式类型，保守处理
                    break;
            }
        }

        // 处理会引入新绑定的表达式
        private static void CollectFreeVariablesWithScope(Expr expr, HashSet<string> bound, HashSet<string> free)
        {
            switch (expr)
            {
                case LetExpr letExpr:
                    CollectFreeVariables(letExpr.Value, bound, free);
                    bound.Add(letExpr.Name); // 后续代码可以看到这个let
                    break;

                case VarExpr varExpr:
                    CollectFreeVariables(varExpr.Value, bound, free);
                    bound.Add(varExpr.Name); // 后续代码可以看到这个var
                    break;

                default:
                    CollectFreeVariables(expr, bound, free);
                    break;
            }
        }
    }

    /// <summary>
    /// 轻量级闭包：只存储实际捕获的变量
    /// </summary>
    public class LightweightClosure
    {
        public Scope ConvertScope()
        {
            return new Scope(this);
        }

        /// <summary>
        /// 捕获的变量（变量名 -> 值）
        /// </summary>
        private readonly Dictionary<string, VariableInfo> _capturedVariables;

        /// <summary>
        /// 父闭包（用于嵌套Lambda）
        /// </summary>
        public LightweightClosure? Parent { get; }

        public LightweightClosure(Dictionary<string, VariableInfo> capturedVariables, LightweightClosure? parent = null)
        {
            _capturedVariables = capturedVariables ?? new Dictionary<string, VariableInfo>();
            Parent = parent;
        }

        /// <summary>
        /// 尝试获取变量
        /// </summary>
        public bool TryGetValue(string name, out VariableInfo? info)
        {
            if (_capturedVariables.TryGetValue(name, out info))
            {
                return true;
            }

            if (Parent != null)
            {
                return Parent.TryGetValue(name, out info);
            }

            info = null;
            return false;
        }

        /// <summary>
        /// 获取所有捕获的变量（用于创建调用作用域）
        /// </summary>
        public Dictionary<string, VariableInfo> GetAllCapturedVariables()
        {
            var result = new Dictionary<string, VariableInfo>();

            // 先添加父闭包的变量
            if (Parent != null)
            {
                foreach (var (key, value) in Parent.GetAllCapturedVariables())
                {
                    result[key] = value;
                }
            }

            // 当前闭包的变量会覆盖父闭包的同名变量
            foreach (var (key, value) in _capturedVariables)
            {
                result[key] = value;
            }

            return result;
        }

        /// <summary>
        /// 从作用域创建轻量级闭包
        /// </summary>
        public static LightweightClosure CreateFromScope(
            Scope scope,
            HashSet<string> variablesToCapture,
            LightweightClosure? parentClosure = null)
        {
            var capturedVars = new Dictionary<string, VariableInfo>();

            // 遍历需要捕获的变量
            foreach (var varName in variablesToCapture)
            {
                if (scope.TryGetValue(varName, out var info))
                {
                    // 对于基本类型且不可变的值，进行深拷贝以断开引用
                    if (!info.IsMutable && IsValueType(info.Value))
                    {
                        capturedVars[varName] = new VariableInfo(
                            info.Value.Copy(),
                            false,
                            IsCaptured: true
                        );
                    }
                    else
                    {
                        // 对于可变或引用类型，保持引用（但标记为已捕获）
                        capturedVars[varName] = new VariableInfo(
                            info.Value,
                            info.IsMutable,
                            IsCaptured: true
                        );
                    }
                }
            }

            return new LightweightClosure(capturedVars, parentClosure);
        }

        /// <summary>
        /// 判断是否为值类型（可以安全拷贝）
        /// </summary>
        private static bool IsValueType(Value value)
        {
            return value is NumberValue or StringValue or BoolValue or NullValue;
        }
    }

}
