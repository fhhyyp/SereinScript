using ScriptLang.Parser;

namespace ScriptLang.Lsp.Analysis;

/// <summary>
/// 模块成员信息
/// </summary>
public sealed class ModuleMemberInfo
{
    public string Name { get; init; } = "";
    public bool IsProperty { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// 模块成员提供器 — 提供 import 模块对象的成员
/// </summary>
public static class ModuleMemberProvider
{
    private static readonly Dictionary<string, List<ModuleMemberInfo>> _scriptCache = new(StringComparer.OrdinalIgnoreCase);

    // ==================== system 模块（硬编码） ====================

    private static readonly Dictionary<string, List<ModuleMemberInfo>> _systemModules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["file"] = new() { M("read",false,"Task<String> read(path)"), M("readAsync",false,"Task<String> readAsync(path)"), M("write",false,"void write(path,content)"), M("writeAsync",false,"Task writeAsync(path,content)"), M("readDir",false,"Array readDir(path)"), M("exists",false,"Bool exists(path)"), M("mkDir",false,"void mkDir(path)"), M("remove",false,"void remove(path)"), M("stat",false,"Object stat(path)"), M("chDir",false,"void chDir(path)"), M("cwd",true,"(property) cwd") },
        ["console"] = new() { M("log",false,"void log(value)"), M("error",false,"void error(value)"), M("readLine",false,"Task<String> readLine()"), M("clear",false,"void clear()"), M("time",false,"void time(label)"), M("timeEnd",false,"void timeEnd(label)"), M("colors",true,"(property) colors") },
        ["path"] = new() { M("join",false,"String join(paths)"), M("resolve",false,"String resolve(path)"), M("dirname",false,"String dirname(path)"), M("basename",false,"String basename(path,ext?)"), M("extname",false,"String extname(path)"), M("normalize",false,"String normalize(path)"), M("separator",true,"(property) separator"), M("delimiter",true,"(property) delimiter") },
        ["json"] = new() { M("stringify",false,"String stringify(value,indent?)"), M("parse",false,"any parse(json)") },
        ["network"] = new() { M("httpGet",false,"Task<Object> httpGet(url,options)"), M("httpPost",false,"Task<Object> httpPost(url,data,contentType?)") },
        ["timer"] = new() { M("sleep",false,"Task sleep(ms)"), M("setTimeout",false,"setTimeout(callback,ms)"), M("setInterval",false,"setInterval(callback,ms)"), M("clearTimer",false,"void clearTimer(timer)") },
        ["crypto"] = new() { M("hash",false,"String hash(algo,data)"), M("hmac",false,"String hmac(algo,data,key)"), M("randomBytes",false,"String randomBytes(len)"), M("randomString",false,"String randomString(len)"), M("uuid",false,"String uuid()"), M("algorithms",true,"(property) algorithms") },
        ["process"] = new() { M("execute",false,"Task<int> execute(cmd)"), M("exit",false,"void exit(code?)"), M("env",false,"Object env()"), M("chDir",false,"void chDir(path)"), M("argv",true,"(property) argv"), M("pid",true,"(property) pid"), M("cwd",true,"(property) cwd"), M("uptime",true,"(property) uptime") },
    };

    private static ModuleMemberInfo M(string name, bool isProp, string desc) => new() { Name = name, IsProperty = isProp, Description = desc };

    public static List<ModuleMemberInfo> GetSystemModuleMembers(string moduleName) =>
        _systemModules.TryGetValue(moduleName, out var m) ? m : [];

    // ==================== .script 导出解析 ====================

    /// <summary>
    /// 解析 .script 文件的导出成员。
    /// 从 return { key = value, ... } 提取键名，并尝试为每个键推断其成员。
    /// </summary>
    public static List<ModuleMemberInfo> GetScriptExportMembers(string scriptPath, string memberName)
    {
        if (!File.Exists(scriptPath)) return [];

        string cacheKey = $"{scriptPath}::{memberName}";
        if (_scriptCache.TryGetValue(cacheKey, out var cached)) return cached;

        var members = ParseAndResolve(scriptPath, memberName);
        _scriptCache[cacheKey] = members;
        return members;
    }

    public static void ClearCache() => _scriptCache.Clear();

    /// <summary>清除指定脚本文件的缓存（文件变更时调用）</summary>
    public static void ClearCacheForFile(string scriptPath)
    {
        string normalizedPath = Path.GetFullPath(scriptPath);
        var keysToRemove = _scriptCache.Keys
            .Where(k => k.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var key in keysToRemove)
            _scriptCache.Remove(key);
    }

    // ==================== 核心解析 ====================

    private static List<ModuleMemberInfo> ParseAndResolve(string scriptPath, string memberName)
    {
        try
        {
            string source = File.ReadAllText(scriptPath);
            var lexer = new ScriptLang.Lexer.Lexer(source, scriptPath);
            var tokens = lexer.ScanTokens();
            var parser = new ScriptLang.Parser.Parser(tokens, scriptPath);
            var ast = parser.Parse();

            if (ast is not ProgramExpr program) return [];

            // 1. 找到 return { ... } 中的导出键
            ObjectLiteralExpr? returnObj = null;
            foreach (var stmt in program.Statements)
            {
                if (stmt is ReturnExpr ret && ret.Value is ObjectLiteralExpr obj)
                {
                    returnObj = obj;
                    break;
                }
            }
            if (returnObj == null) return [];

            // 2. 找到目标成员（如 "store"）对应的属性值
            Expr? targetValue = null;
            foreach (var prop in returnObj.Properties)
            {
                if (prop.Key == memberName)
                {
                    targetValue = prop.Value;
                    break;
                }
            }
            if (targetValue == null) return []; // 成员名不存在

            // 3. 如果值是 IdentifierExpr（如 store），查找其 let/var 定义
            if (targetValue is IdentifierExpr id)
            {
                targetValue = FindDefinition(program, id.Name);
            }

            // 4. 从值中推断成员
            return InferMembers(targetValue);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>在 AST 中查找 let/var 定义的值</summary>
    private static Expr? FindDefinition(ProgramExpr program, string name)
    {
        foreach (var stmt in program.Statements)
        {
            if (stmt is LetExpr let && let.Name == name)
                return let.Value;
            if (stmt is VarExpr var && var.Name == name)
                return var.Value;
        }
        return null;
    }

    /// <summary>从表达式中推断成员列表</summary>
    private static List<ModuleMemberInfo> InferMembers(Expr? expr)
    {
        if (expr == null) return [];

        switch (expr)
        {
            // 直接的对象字面量: { state = ..., getters = ..., actions = ... }
            case ObjectLiteralExpr obj:
                var members = new List<ModuleMemberInfo>();
                foreach (var prop in obj.Properties)
                {
                    bool isFunc = prop.Value is LambdaExpr;
                    members.Add(new ModuleMemberInfo
                    {
                        Name = prop.Key,
                        IsProperty = !isFunc,
                        Description = isFunc ? $"function {prop.Key}" : $"(property) {prop.Key}"
                    });
                }
                return members;

            // CallExpr: 函数调用的返回值无法静态确定（图灵完备性限制），
            // 不猜测参数形状作为返回值。回退到通用 ObjectValue 成员。
            case CallExpr:
                return [];
        }

        return [];
    }

    // ==================== 类型推断 + 原型成员 ====================

    private static readonly Dictionary<string, List<ModuleMemberInfo>> _prototypeMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = new() {
            PM("length",true,"int"),
            PM("toUpper",false,"String toUpper()"),
            PM("toLower",false,"String toLower()"),
            PM("trim",false,"String trim()"),
            PM("split",false,"Array split(separator)"),
            PM("substring",false,"String substring(start,length?)"),
            PM("contains",false,"Bool contains(value)"),
        },
        ["array"] = new() {
            PM("count",true,"int"),
            PM("length",true,"int"),
            PM("add",false,"void add(item)"),
            PM("first",false,"any first()"),
            PM("last",false,"any last()"),
            PM("select",false,"Array select(fn)"),
            PM("where",false,"Array where(fn)"),
            PM("orderBy",false,"Array orderBy()"),
            PM("orderByDesc",false,"Array orderByDesc()"),
            PM("toList",false,"Array toList()"),
        },
        ["object"] = new() {
            PM("count",true,"int"),
            PM("keys",false,"Array keys()"),
            PM("values",false,"Array values()"),
            PM("has",false,"Bool has(key)"),
            PM("get",false,"any get(key)"),
            PM("set",false,"void set(key,value)"),
            PM("containsKey",false,"Bool containsKey(key)"),
            PM("remove",false,"Bool remove(key)"),
            PM("clear",false,"void clear()"),
        },
    };
    private static ModuleMemberInfo PM(string name, bool isProp, string desc) => new() { Name = name, IsProperty = isProp, Description = desc };

    /// <summary>根据推断类型获取原型成员列表</summary>
    public static List<ModuleMemberInfo> GetPrototypeMembers(string typeName)
        => _prototypeMembers.TryGetValue(typeName, out var m) ? m : [];

    /// <summary>推断表达式的运行时类型名</summary>
    public static string InferExprType(Expr? expr)
    {
        return expr switch
        {
            LiteralExpr lit => lit.Value switch
            {
                string => "string",
                int or long or float or double or decimal => "number",
                bool => "bool",
                null => "unknown",
                _ => "unknown"
            },
            ArrayLiteralExpr => "array",
            ObjectLiteralExpr => "object",
            LambdaExpr => "function",
            IdentifierExpr id => "unknown", // 动态类型，无法确定
            CallExpr => "unknown",
            _ => "unknown"
        };
    }

    /// <summary>
    /// 在 AST 中查找给定变量名的初始值表达式
    /// </summary>
    public static Expr? FindVariableInit(ProgramExpr program, string varName)
    {
        // 顶层声明
        foreach (var stmt in program.Statements)
        {
            if (stmt is LetExpr let && let.Name == varName) return let.Value;
            if (stmt is VarExpr var && var.Name == varName) return var.Value;
        }
        return null;
    }
}
