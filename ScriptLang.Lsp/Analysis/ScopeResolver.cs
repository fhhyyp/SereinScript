using ScriptLang.Parser;

namespace ScriptLang.Lsp.Analysis;

/// <summary>
/// 作用域遍历器 — 递归遍历 AST 构建作用域树和符号表
/// </summary>
public static class ScopeResolver
{
    /// <summary>
    /// 从 AST 构建完整的作用域树和符号表
    /// </summary>
    /// <param name="ast">程序 AST 根节点</param>
    /// <param name="builtinNames">内置函数名集合</param>
    /// <returns>全局作用域（包含所有嵌套作用域）</returns>
    public static Scope BuildScopes(ProgramExpr ast, HashSet<string> builtinNames, string? basePath = null)
    {
        var globalScope = new Scope(null) { Node = ast };

        foreach (var name in builtinNames)
        {
            globalScope.Symbols[name] = new SymbolInfo(name, ScriptSymbolKind.Builtin, ast.SourceSpan)
            {
                ParentScope = globalScope
            };
        }

        ResolveProgram(ast, globalScope, basePath);
        return globalScope;
    }

    public static string? CurrentBasePath { get; private set; }

    /// <summary>
    /// 遍历 ProgramExpr — 处理顶层声明
    /// </summary>
    private static void ResolveProgram(ProgramExpr program, Scope scope, string? basePath = null)
    {
        foreach (var stmt in program.Statements)
        {
            ResolveTopLevel(stmt, scope, basePath);
        }
    }

    private static void ResolveTopLevel(Expr expr, Scope scope, string? basePath = null)
    {
        switch (expr)
        {
            case LetExpr let:
                scope.Symbols[let.Name] = new SymbolInfo(let.Name, ScriptSymbolKind.LetVariable, let.SourceSpan) { ParentScope = scope };
                ResolveExpression(let.Value, scope);
                break;

            case VarExpr var:
                scope.Symbols[var.Name] = new SymbolInfo(var.Name, ScriptSymbolKind.VarVariable, var.SourceSpan) { ParentScope = scope };
                ResolveExpression(var.Value, scope);
                break;

            case ImportStmt import:
                foreach (var (member, alias) in import.Members)
                {
                    var name = alias ?? member;
                    List<ModuleMemberInfo>? moduleMembers = null;

                    if (string.Equals(import.FilePath, "system", StringComparison.OrdinalIgnoreCase))
                    {
                        moduleMembers = ModuleMemberProvider.GetSystemModuleMembers(name);
                    }
                    else if (import.FilePath.EndsWith(".script", StringComparison.OrdinalIgnoreCase) && basePath != null)
                    {
                        // 解析相对路径 → 绝对路径 → 解析目标文件的导出成员
                        string targetPath = Path.GetFullPath(Path.Combine(basePath, import.FilePath));
                        moduleMembers = ModuleMemberProvider.GetScriptExportMembers(targetPath, name);
                    }

                    scope.Symbols[name] = new SymbolInfo(name, ScriptSymbolKind.Import, import.SourceSpan)
                    {
                        ParentScope = scope,
                        Detail = $"from \"{import.FilePath}\"",
                        ModuleMembers = moduleMembers
                    };
                }
                break;

            case AssignExpr assign:
                ResolveExpression(assign.Value, scope);
                break;

            default:
                ResolveExpression(expr, scope);
                break;
        }
    }

    /// <summary>
    /// 递归解析表达式，遇到 Block/Lambda/For 时创建子作用域
    /// </summary>
    public static void ResolveExpression(Expr? expr, Scope scope)
    {
        if (expr == null) return;

        switch (expr)
        {
            case BlockExpr block:
                var blockScope = new Scope(scope) { Node = block };
                foreach (var stmt in block.Statements)
                {
                    ResolveTopLevel(stmt, blockScope);
                }
                break;

            case LambdaExpr lambda:
                var lambdaScope = new Scope(scope) { Node = lambda };
                // 注册参数
                foreach (var param in lambda.Params)
                {
                    lambdaScope.Symbols[param] = new SymbolInfo(param, ScriptSymbolKind.Parameter, lambda.SourceSpan) { ParentScope = lambdaScope };
                }
                // 递归解析 Lambda 体
                ResolveExpression(lambda.Body, lambdaScope);
                break;

            case ForExpr forExpr:
                var forScope = new Scope(scope) { Node = forExpr };
                // 注册循环变量
                forScope.Symbols[forExpr.VarName] = new SymbolInfo(forExpr.VarName, ScriptSymbolKind.VarVariable, forExpr.SourceSpan) { ParentScope = forScope };
                ResolveExpression(forExpr.Iterable, forScope);
                ResolveExpression(forExpr.Body, forScope);
                break;

            case WhenExpr whenExpr:
                ResolveExpression(whenExpr.Value, scope);
                foreach (var clause in whenExpr.Clauses)
                {
                    ResolveExpression(clause.Pattern, scope);
                    ResolveExpression(clause.Body, scope);
                }
                if (whenExpr.OtherClause != null)
                    ResolveExpression(whenExpr.OtherClause.Body, scope);
                break;

            case LetExpr let:
                scope.Symbols[let.Name] = new SymbolInfo(let.Name, ScriptSymbolKind.LetVariable, let.SourceSpan) { ParentScope = scope };
                ResolveExpression(let.Value, scope);
                break;

            case VarExpr var:
                scope.Symbols[var.Name] = new SymbolInfo(var.Name, ScriptSymbolKind.VarVariable, var.SourceSpan) { ParentScope = scope };
                ResolveExpression(var.Value, scope);
                break;

            case BinaryExpr binary:
                ResolveExpression(binary.Left, scope);
                ResolveExpression(binary.Right, scope);
                break;

            case UnaryExpr unary:
                ResolveExpression(unary.Expr, scope);
                break;

            case ConditionalExpr cond:
                ResolveExpression(cond.Cond, scope);
                ResolveExpression(cond.Then, scope);
                ResolveExpression(cond.Else, scope);
                break;

            case IfExpr ifExpr:
                ResolveExpression(ifExpr.Cond, scope);
                ResolveExpression(ifExpr.Then, scope);
                ResolveExpression(ifExpr.Else, scope);
                break;

            case CallExpr call:
                ResolveExpression(call.Target, scope);
                foreach (var arg in call.Args)
                    ResolveExpression(arg, scope);
                break;

            case ReturnExpr ret:
                if (ret.Value != null) ResolveExpression(ret.Value, scope);
                break;

            case ArrayLiteralExpr arr:
                foreach (var elem in arr.Elements)
                    ResolveExpression(elem, scope);
                break;

            case ObjectLiteralExpr obj:
                foreach (var prop in obj.Properties)
                    ResolveExpression(prop.Value, scope);
                break;

            case MemberAccessExpr member:
                ResolveExpression(member.Target, scope);
                break;

            case MemberAssignExpr memberAssign:
                ResolveExpression(memberAssign.Target, scope);
                ResolveExpression(memberAssign.Value, scope);
                break;

            case IndexAccessExpr index:
                ResolveExpression(index.Target, scope);
                ResolveExpression(index.Index, scope);
                break;

            case IndexAssignExpr indexAssign:
                ResolveExpression(indexAssign.Target, scope);
                ResolveExpression(indexAssign.Index, scope);
                ResolveExpression(indexAssign.Value, scope);
                break;

            case AssignExpr assign:
                ResolveExpression(assign.Value, scope);
                break;

            // 字面量、标识符、错误节点 — 不做作用域操作
            case LiteralExpr:
            case IdentifierExpr:
            case ErrorExpr:
            case ProgramExpr:
                break;
        }
    }

    /// <summary>
    /// 在作用域树中查找包含给定偏移量的最深层作用域
    /// </summary>
    public static Scope? FindScopeAt(Scope root, int offset)
    {
        if (root.Node == null || !Utilities.PositionMapper.IsInSpan(root.Node.SourceSpan, offset))
            return null;

        // 先查子作用域（更深层）
        foreach (var child in root.Children)
        {
            var found = FindScopeAt(child, offset);
            if (found != null) return found;
        }

        return root;
    }
}
