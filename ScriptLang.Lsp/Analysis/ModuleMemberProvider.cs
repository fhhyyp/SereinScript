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

            // 构建此脚本的 import 映射表（变量名 → 文件路径）
            var importMap = BuildImportMap(program);

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
            if (targetValue == null) return [];

            // 3. 如果值是 IdentifierExpr（如 store），查找其 let/var 定义
            string? targetVarName = null;
            if (targetValue is IdentifierExpr id)
            {
                targetVarName = id.Name;
                targetValue = FindDefinition(program, id.Name);
            }

            // 4. 注解优先：检查定义变量上的 @type 注解
            if (targetVarName != null)
            {
                var annotated = AnnotationParser.FindAnnotation(source, targetVarName);
                if (annotated is { Count: > 0 }) return annotated;
            }

            // 5. CallExpr 跨文件追踪：如果值来自导入函数的调用，查 @returns 注解
            if (targetValue is CallExpr call && call.Target is IdentifierExpr callId)
            {
                global::System.Console.Error.WriteLine($"[LSP.Annotation] Attempting cross-file trace: '{callId.Name}' via importMap (keys: {string.Join(",", importMap.Keys)})");
                var crossFile = ResolveCrossFileCall(callId.Name, importMap, source, program);
                if (crossFile is { Count: > 0 }) return crossFile;
                global::System.Console.Error.WriteLine($"[LSP.Annotation] Cross-file trace returned empty for '{callId.Name}'");
            }

            // 6. 回退到 AST 结构推断
            var inferred = InferMembers(targetValue);
            global::System.Console.Error.WriteLine($"[LSP.Annotation] Fallback InferMembers returned {inferred.Count} items for '{memberName}'");
            return inferred;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>构建当前脚本的 import 映射表：导入变量名 → 绝对文件路径</summary>
    private static Dictionary<string, string> BuildImportMap(ProgramExpr program)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var stmt in program.Statements)
        {
            if (stmt is ImportStmt import)
            {
                foreach (var (member, alias) in import.Members)
                {
                    var name = alias ?? member;
                    map[name] = import.FilePath;
                }
            }
        }
        return map;
    }

    /// <summary>
    /// 跨文件追踪：当 CallExpr 调用的是一个导入函数时，
    /// 逐层追踪到函数定义所在的源文件，查找 @returns 注解。
    /// </summary>
    private static List<ModuleMemberInfo>? ResolveCrossFileCall(
        string funcName,
        Dictionary<string, string> importMap,
        string currentSource,
        ProgramExpr currentProgram)
    {
        // 在当前文件的 importMap 中查找 funcName
        if (!importMap.TryGetValue(funcName, out string? importPath))
        {
            global::System.Console.Error.WriteLine($"[LSP.Annotation] '{funcName}' not in importMap");
            return null;
        }
        if (!importPath.EndsWith(".script", StringComparison.OrdinalIgnoreCase))
        {
            global::System.Console.Error.WriteLine($"[LSP.Annotation] '{funcName}' import path '{importPath}' not .script");
            return null;
        }

        // 解析 import 路径为绝对路径
        string resolvedPath;
        if (Path.IsPathRooted(importPath))
            resolvedPath = Path.GetFullPath(importPath);
        else
        {
            string currentFilePath = currentProgram.SourceSpan.FilePath;
            global::System.Console.Error.WriteLine($"[LSP.Annotation] Resolving relative import: base='{currentFilePath}' import='{importPath}'");
            string? baseDir = Path.GetDirectoryName(Path.GetFullPath(currentFilePath));
            if (baseDir == null) { global::System.Console.Error.WriteLine($"[LSP.Annotation] baseDir is null"); return null; }
            resolvedPath = Path.GetFullPath(Path.Combine(baseDir, importPath));
        }

        global::System.Console.Error.WriteLine($"[LSP.Annotation] Resolved path: '{resolvedPath}', exists={File.Exists(resolvedPath)}");
        if (!File.Exists(resolvedPath)) return null;

        // 读取目标文件，查找函数定义和 @returns 注解
        try
        {
            string targetSource = File.ReadAllText(resolvedPath);
            var annotated = AnnotationParser.FindAnnotation(targetSource, funcName);
            if (annotated is { Count: > 0 }) return annotated;

            // 递归：目标文件的函数可能也是从更深层的 import 来的
            var targetAst = ParseFile(targetSource, resolvedPath);
            if (targetAst is ProgramExpr targetProgram)
            {
                var targetImportMap = BuildImportMap(targetProgram);
                var targetDef = FindDefinition(targetProgram, funcName);
                if (targetDef is CallExpr nestedCall && nestedCall.Target is IdentifierExpr nestedId)
                {
                    return ResolveCrossFileCall(nestedId.Name, targetImportMap, targetSource, targetProgram);
                }
            }
        }
        catch { }

        return null;
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

            case CallExpr:
                return [];
        }

        return [];
    }

    /// <summary>解析文件为 AST</summary>
    private static Expr? ParseFile(string source, string filePath)
    {
        try
        {
            var lexer = new ScriptLang.Lexer.Lexer(source, filePath);
            var tokens = lexer.ScanTokens();
            var parser = new ScriptLang.Parser.Parser(tokens, filePath);
            return parser.Parse();
        }
        catch { return null; }
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
