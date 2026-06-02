using ScriptLang.Parser;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
                    //var letBound = new HashSet<string>(bound) { letExpr.Name };
                    // 注意：let的作用域不包含自身初始值，但后续代码可以看到
                    break;

                case VarExpr varExpr:
                    // var绑定：同let
                    CollectFreeVariables(varExpr.Value, bound, free);
                    //var varBound = new HashSet<string>(bound) { varExpr.Name };
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


    public class LightweightClosure : IClosureContext
    {
        private readonly Dictionary<string, VariableInfo> _captured;

        public LightweightClosure(Dictionary<string, VariableInfo> capturedVariables)
        {
            _captured = capturedVariables;
        }

        public static LightweightClosure CreateFromScope(IClosureContext scope, HashSet<string> vars)
        {
            var captured = new Dictionary<string, VariableInfo>();

            foreach (var name in vars)
            {
                if (scope.TryGetValue(name, out var info))
                {
                    info.IsCaptured = true;

                    captured[name] = info;
                }
            }
            return new LightweightClosure(captured);
        }

        public void Clear()
        {
            foreach (var pair in _captured)
            {
                if (!pair.Value.IsCaptured)
                {
                    pair.Value.Cell.Value = Value.Null;
                }
            }

        }

        public bool TryGetValue(string name, [NotNullWhen(true)] out VariableInfo? info)
        {
            if (_captured.TryGetValue(name,out info))
            {
                return true;
            }
            return false;
        }

        public bool Exists(string name)
        {
            return _captured.ContainsKey(name);
        }


        public void Set(string name, Value value)
        {
            if (!_captured.TryGetValue(name, out var info))
            {
                throw new RuntimeException($"当前作用域中不存在变量 '{name}'");
            }
            if (!info.IsMutable)
            {
                throw new RuntimeException($"当前变量 '{name}' 不可变");
            }
            info?.Cell.Value = value; 
        }
    }



    /// <summary>
    /// 作用域（支持嵌套和闭包）
    /// </summary>
    public class Scope : IClosureContext
    {
        public Guid Guid { get; private set; } = Guid.NewGuid();

        private readonly Dictionary<string, VariableInfo> _variables = new();

        /// <summary>
        /// 父作用域
        /// </summary>
        public IClosureContext? Parent { get; }

        public Scope(IClosureContext? parent = null)
        {
            Parent = parent;
            //BuiltinFunctions.RegisterAll(this);
        }

        /// <summary>
        /// 创建子作用域
        /// </summary>
        public IClosureContext CreateChildScope() => new Scope(this);

        /// <summary>
        /// 清除作用域
        /// </summary>
        public void Clear()
        {
            foreach (var pair in _variables)
            {
                if (!pair.Value.IsCaptured)
                {
                    pair.Value.Cell.Value = Value.Null;
                }
            }

            _variables.Clear();
        }

        /// <summary>
        /// 定义函数
        /// </summary>
        public void DefineFunction(FunctionValue func) 
            => Define(func.Name, func, isMutable: false);

        /// <summary>
        /// 定义变量
        /// </summary>
        public VariableInfo Define(string name, Value value, bool isMutable = true)
        {
            if (_variables.ContainsKey(name))
            {
                throw new RuntimeException($"变量 '{name}' 已在此作用域中定义");
            }
            //_variables[name] = new VariableInfo(value, isMutable);
            var variable = new VariableInfo(
                                    new VariableCell(value),
                                    isMutable);
            _variables[name] = variable;
            return variable;
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
            _variables[name] = new VariableInfo(new VariableCell(value), isMutable);
        }

        /// <inheritdoc />
        bool IClosureContext.TryGetValue(string name, [NotNullWhen(true)] out VariableInfo? info)
        {
            return TryGetValue(name, out info);
        }

        /// <summary>
        /// 检查当前作用域是否存在某个变量
        /// </summary>
        /// <param name="varName"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        internal bool TryGetValue(string varName, [NotNullWhen(true)] out VariableInfo? info)
        {
            if (_variables.TryGetValue(varName, out info))
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
                info.Cell.Value = value;
                return;
            }

            if (Parent?.TryGetValue(name, out var parentInfo) == true)
            {
                if (!parentInfo.IsMutable)
                {
                    throw new RuntimeException($"无法为不可变变量 '{name}' 赋值");
                }
                parentInfo.Cell.Value = value;
                return;
            }

            throw new RuntimeException($"当前作用域未定义的变量 '{name}'");
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

    }



    /// <summary>
    /// 闭包上下文
    /// </summary>
    public interface IClosureContext
    {
        /// <summary>
        /// 清理作用域（用于Lambda调用结束后清理捕获的变量）
        /// </summary>
        void Clear();

        /// <summary>
        /// 判断变量是否存在（在当前或父作用域中）
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        bool Exists(string name);

        /// <summary>
        /// 设置变量（仅对可变变量）
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        void Set(string name, Value value);

        /// <summary>
        /// 获取变量
        /// </summary>
        /// <param name="name">变量名称</param>
        /// <param name="info">返回的变量</param>
        /// <returns>是否成功返回了变量</returns>
        bool TryGetValue(string name, [NotNullWhen(true)] out VariableInfo? info);
    }


    /// <summary>
    /// 变量容器
    /// </summary>
    public sealed class VariableCell(Value value)
    {
        /// <summary>
        /// 变量值
        /// </summary>
        public Value Value = value;
    }

    /// <summary>
    /// 变量信息
    /// </summary>

    public sealed class VariableInfo
    {
        /// <summary>
        /// 存储变量值的容器（支持闭包捕获）
        /// </summary>
        public VariableCell Cell { get; }

        /// <summary>
        /// 是否可变（var定义的变量为true，let定义的变量为false）
        /// </summary>
        public bool IsMutable { get; set; }

        /// <summary>
        /// 是否被闭包捕获（如果是被捕获的变量，在调用Lambda时需要特殊处理）
        /// </summary>
        public bool IsCaptured { get; set; }


        public VariableInfo(VariableCell cell, bool isMutable, bool isCaptured = false)
        {
            Cell = cell;
            IsMutable = isMutable;
            IsCaptured = isCaptured;
        }
    }

}
