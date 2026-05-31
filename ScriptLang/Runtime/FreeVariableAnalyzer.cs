using global::ScriptLang.Parser;


namespace ScriptLang.Runtime
{


    /// <summary>
    /// 自由变量分析器 - 找出Lambda中引用但未在参数中定义的变量
    /// </summary>
    public class FreeVariableAnalyzer
    {
        private readonly HashSet<string> _parameters;
        private readonly HashSet<string> _localVariables;
        private readonly HashSet<string> _freeVariables;
        private readonly Stack<HashSet<string>> _scopeStack;

        public FreeVariableAnalyzer(List<string> parameters)
        {
            _parameters = new HashSet<string>(parameters);
            _localVariables = new HashSet<string>();
            _freeVariables = new HashSet<string>();
            _scopeStack = new Stack<HashSet<string>>();
        }

        /// <summary>
        /// 分析表达式树，返回自由变量集合
        /// </summary>
        public HashSet<string> Analyze(Expr body)
        {
            PushScope(); // 函数体作用域
            AnalyzeExpression(body);
            PopScope();

            // 移除参数（参数不是自由变量）
            _freeVariables.ExceptWith(_parameters);

            return _freeVariables;
        }

        private void PushScope()
        {
            _scopeStack.Push(new HashSet<string>());
        }

        private void PopScope()
        {
            if (_scopeStack.Count > 0)
            {
                var scope = _scopeStack.Pop();
                _localVariables.UnionWith(scope);
            }
        }

        private void AddVariable(string name)
        {
            if (_scopeStack.Count > 0)
            {
                _scopeStack.Peek().Add(name);
            }
        }

        private bool IsLocalVariable(string name)
        {
            // 检查是否在参数或局部作用域中定义
            if (_parameters.Contains(name))
                return true;

            foreach (var scope in _scopeStack)
            {
                if (scope.Contains(name))
                    return true;
            }

            return false;
        }

        private void AnalyzeExpression(Expr expr)
        {
            switch (expr)
            {
                case IdentifierExpr identifier:
                    AnalyzeIdentifier(identifier);
                    break;

                case LiteralExpr _:
                    break;

                case LetExpr let:
                    AnalyzeLet(let);
                    break;

                case VarExpr var:
                    AnalyzeVar(var);
                    break;

                case AssignExpr assign:
                    AnalyzeAssign(assign);
                    break;

                case BinaryExpr binary:
                    AnalyzeBinary(binary);
                    break;

                case UnaryExpr unary:
                    AnalyzeUnary(unary);
                    break;

                case ConditionalExpr conditional:
                    AnalyzeConditional(conditional);
                    break;

                case IfExpr ifExpr:
                    AnalyzeIf(ifExpr);
                    break;

                case WhenExpr whenExpr:
                    AnalyzeWhen(whenExpr);
                    break;

                case ForExpr forExpr:
                    AnalyzeFor(forExpr);
                    break;

                case LambdaExpr lambda:
                    AnalyzeLambda(lambda);
                    break;

                case CallExpr call:
                    AnalyzeCall(call);
                    break;

                case BlockExpr block:
                    AnalyzeBlock(block);
                    break;

                case ArrayLiteralExpr array:
                    AnalyzeArray(array);
                    break;

                case ObjectLiteralExpr obj:
                    AnalyzeObject(obj);
                    break;

                case MemberAccessExpr memberAccess:
                    AnalyzeExpression(memberAccess.Target);
                    break;

                case MemberAssignExpr memberAssign:
                    AnalyzeExpression(memberAssign.Target);
                    AnalyzeExpression(memberAssign.Value);
                    break;

                case IndexAccessExpr indexAccess:
                    AnalyzeExpression(indexAccess.Target);
                    AnalyzeExpression(indexAccess.Index);
                    break;

                case IndexAssignExpr indexAssign:
                    AnalyzeExpression(indexAssign.Target);
                    AnalyzeExpression(indexAssign.Index);
                    AnalyzeExpression(indexAssign.Value);
                    break;

                case ReturnExpr returnExpr:
                    if (returnExpr.Value != null)
                        AnalyzeExpression(returnExpr.Value);
                    break;

                case ErrorExpr _:
                    break;

               
                case ImportStmt _:
                    break;

                default:
                    break;
            }
        }

        private void AnalyzeIdentifier(IdentifierExpr identifier)
        {
            // 如果不是局部变量或参数，则为自由变量
            if (!IsLocalVariable(identifier.Name))
            {
                _freeVariables.Add(identifier.Name);
            }
        }

        private void AnalyzeLet(LetExpr let)
        {
            AnalyzeExpression(let.Value);
            AddVariable(let.Name); // 将let变量添加到当前作用域
        }

        private void AnalyzeVar(VarExpr varExpr)
        {
            AnalyzeExpression(varExpr.Value);
            AddVariable(varExpr.Name); // 将var变量添加到当前作用域
        }

        private void AnalyzeAssign(AssignExpr assign)
        {
            AnalyzeExpression(assign.Value);
            // 赋值不定义新变量，但标识符可能是自由变量
            if (!IsLocalVariable(assign.Name))
            {
                _freeVariables.Add(assign.Name);
            }
        }

        private void AnalyzeBinary(BinaryExpr binary)
        {
            AnalyzeExpression(binary.Left);
            AnalyzeExpression(binary.Right);
        }

        private void AnalyzeUnary(UnaryExpr unary)
        {
            AnalyzeExpression(unary.Expr);
        }

        private void AnalyzeConditional(ConditionalExpr conditional)
        {
            AnalyzeExpression(conditional.Cond);
            AnalyzeExpression(conditional.Then);
            AnalyzeExpression(conditional.Else);
        }

        private void AnalyzeIf(IfExpr ifExpr)
        {
            AnalyzeExpression(ifExpr.Cond);
            AnalyzeExpression(ifExpr.Then);
            AnalyzeExpression(ifExpr.Else);
        }

        private void AnalyzeWhen(WhenExpr whenExpr)
        {
            AnalyzeExpression(whenExpr.Value);
            foreach (var clause in whenExpr.Clauses)
            {
                AnalyzeExpression(clause.Pattern);
                AnalyzeExpression(clause.Body);
            }
        }

        private void AnalyzeFor(ForExpr forExpr)
        {
            AnalyzeExpression(forExpr.Iterable);

            // 创建循环作用域
            PushScope();
            AddVariable(forExpr.VarName); // 循环变量是局部的
            AnalyzeExpression(forExpr.Body);
            PopScope();
        }

        private void AnalyzeLambda(LambdaExpr lambda)
        {
            // 嵌套Lambda - 只分析不捕获（内层Lambda有自己的闭包）
            // 但当前Lambda可能引用外层变量
            var nestedAnalyzer = new FreeVariableAnalyzer(lambda.Params);
            var nestedFreeVars = nestedAnalyzer.Analyze(lambda.Body);

            // 嵌套Lambda使用的自由变量可能是当前Lambda的自由变量
            foreach (var freeVar in nestedFreeVars)
            {
                if (!IsLocalVariable(freeVar))
                {
                    _freeVariables.Add(freeVar);
                }
            }
        }

        private void AnalyzeCall(CallExpr call)
        {
            AnalyzeExpression(call.Target);
            foreach (var arg in call.Args)
            {
                AnalyzeExpression(arg);
            }
        }

        private void AnalyzeBlock(BlockExpr block)
        {
            PushScope();
            foreach (var stmt in block.Statements)
            {
                AnalyzeExpression(stmt);
            }
            PopScope();
        }

        private void AnalyzeArray(ArrayLiteralExpr array)
        {
            foreach (var element in array.Elements)
            {
                AnalyzeExpression(element);
            }
        }

        private void AnalyzeObject(ObjectLiteralExpr obj)
        {
            foreach (var prop in obj.Properties)
            {
                AnalyzeExpression(prop.Value);
            }
        }
    }
}
