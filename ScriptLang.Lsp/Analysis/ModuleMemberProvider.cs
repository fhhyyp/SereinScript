using System.Reflection;
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
/// 模块成员提供器。
/// system 模块通过反射 ScriptLang 程序集自动发现（扫描 [PrototypeExtension] 类），
/// .script 文件通过解析 return { ... } + 注解获取。
/// </summary>
public static class ModuleMemberProvider
{
    private static Dictionary<string, List<ModuleMemberInfo>>? _systemModules;
    private static readonly object _systemLock = new();
    private static readonly Dictionary<string, List<ModuleMemberInfo>> _scriptCache = new(StringComparer.OrdinalIgnoreCase);

    // ==================== system 模块（反射自动发现） ====================

    public static List<ModuleMemberInfo> GetSystemModuleMembers(string moduleName)
    {
        EnsureSystemModulesLoaded();
        return _systemModules!.TryGetValue(moduleName, out var m) ? m : [];
    }

    private static void EnsureSystemModulesLoaded()
    {
        if (_systemModules != null) return;
        lock (_systemLock)
        {
            if (_systemModules != null) return;
            _systemModules = BuildSystemModuleRegistry();
        }
    }

    /// <summary>
    /// 通过反射 ScriptLang 程序集自动发现所有 [PrototypeExtension] 类。
    /// 无需手动维护模块成员列表——增减模块时自动同步。
    /// </summary>
    private static Dictionary<string, List<ModuleMemberInfo>> BuildSystemModuleRegistry()
    {
        var registry = new Dictionary<string, List<ModuleMemberInfo>>(StringComparer.Ordinal);
        var assembly = typeof(ScriptLang.ScriptEngine).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            // 1. 检查 [PrototypeExtension] 属性（按名称匹配，处理 internal 属性）
            bool hasExt = false;
            bool isJsNaming = false; // Js=1, Net=0
            foreach (var ad in type.GetCustomAttributesData())
            {
                if (ad.AttributeType.Name == "PrototypeExtensionAttribute")
                {
                    hasExt = true;
                    foreach (var arg in ad.NamedArguments)
                    {
                        if (arg.MemberName == "NamingFormat")
                            isJsNaming = (arg.TypedValue.Value is int n && n == 1);
                    }
                    break;
                }
            }
            if (!hasExt) continue;

            // 2. 仅处理 ScriptLang.System.* 命名空间的模块（排除 Prototype 类）
            if (type.Namespace == null || !type.Namespace.StartsWith("ScriptLang.System", StringComparison.Ordinal))
                continue;

            // 3. 模块名：类名去掉 "Module" 后缀，首字母小写
            string moduleName = ClassNameToModuleName(type.Name);
            var members = new List<ModuleMemberInfo>();

            // 3. 扫描方法：[PrototypeFunction] / [PrototypeProperty]
            foreach (var method in type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                bool isFunc = false, isProp = false;
                foreach (var ad in method.GetCustomAttributesData())
                {
                    string an = ad.AttributeType.Name;
                    if (an == "PrototypeFunctionAttribute") { isFunc = true; break; }
                    if (an == "PrototypePropertyAttribute") { isProp = true; break; }
                }
                if (!isFunc && !isProp) continue;

                string name = ApplyNamingFormat(method.Name, isJsNaming);

                // 始终先生成方法签名作为第一行
                string signature;
                if (isProp)
                    signature = $"(property) {name}";
                else
                {
                    string retType = GetFriendlyTypeName(method.ReturnType);
                    string paramStr = string.Join(",", method.GetParameters().Where(x => x.ParameterType != typeof(ScriptLang.ScriptEngine))
                        .Select(p => $"{GetFriendlyTypeName(p.ParameterType)} {p.Name}"));
                    signature = $"{retType} {name}({paramStr})";
                }

                // LspDoc 注释作为第二行（如果存在）
                var lspDoc = method.GetCustomAttribute<ScriptLang.LspDocAttribute>();
                string desc = lspDoc != null ? $"{signature}\n{lspDoc.Description}" : signature;

                members.Add(new ModuleMemberInfo { Name = name, IsProperty = isProp, Description = desc });
            }

            if (members.Count > 0)
                registry[moduleName] = members;
        }

        return registry;
    }

    /// <summary>获取可读类型名：Task`1[StringValue] → Task<string>, NumberValue`1[int] → int</summary>
    private static string GetFriendlyTypeName(Type type)
    {
        if (type == typeof(void)) return "void";
        if (type == typeof(Task) || type == typeof(ValueTask)) return "void";

        if (type.IsGenericType)
        {
            string baseName = SimplifyTypeName(type.Name); // Task`1 → Task, ValueTask`1 → ValueTask
            int tick = baseName.IndexOf('`');
            if (tick > 0) baseName = baseName[..tick];

            var args = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
            return $"{baseName}<{args}>";
        }

        return SimplifyTypeName(type.Name);
    }

    /// <summary>类型名简化：StringValue→string, NumberValue→num, BoolValue→bool, 等</summary>
    private static string SimplifyTypeName(string name)
    {
        // 去掉泛型 tick 后缀
        int tick = name.IndexOf('`');
        if (tick > 0) name = name[..tick];

        return name switch
        {
            "StringValue" => "string",
            "BoolValue" => "bool",
            "NullValue" => "null",
            "NumberValue" => "num",
            "ObjectValue" => "object",
            "ArrayValue" => "array",
            "ClrObjectValue" => "object",
            "ClrMethodValue" => "function",
            "FunctionValue" => "function",
            "CompiledFunctionValue" => "function",
            "MutableNumber" => "num",
            "DateTimeValue" => "datetime",
            "TimeSpanValue" => "timespan",
            "Void" => "void",
            "Int32" => "int",
            "Int64" => "long",
            "Single" => "float",
            "Double" => "double",
            "Decimal" => "decimal",
            "String" => "string",
            "Boolean" => "bool",
            "ValueTask" => "Task",
            _ => name
        };
    }

    /// <summary>类名 → 模块名：FileModule → file, NetworkModule → network</summary>
    private static string ClassNameToModuleName(string className)
    {
        if (className.EndsWith("Module", StringComparison.Ordinal))
            className = className[..^6];
        return className.Length > 0 ? char.ToLower(className[0]) + className[1..] : className;
    }

    /// <summary>Js → camelCase, Net → PascalCase</summary>
    private static string ApplyNamingFormat(string methodName, bool isJs)
    {
        if (string.IsNullOrEmpty(methodName)) return methodName;
        return isJs ? char.ToLower(methodName[0]) + methodName[1..] : methodName;
    }

    // ==================== .script 导出 + 注解 ====================

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

    /// <summary>清除指定脚本文件的缓存</summary>
    public static void ClearCacheForFile(string scriptPath)
    {
        string normalizedPath = Path.GetFullPath(scriptPath);
        var keys = _scriptCache.Keys.Where(k => k.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var k in keys) _scriptCache.Remove(k);
    }

    // ==================== 解析 + 推断 ====================

    private static List<ModuleMemberInfo> ParseAndResolve(string scriptPath, string memberName)
    {
        try
        {
            string source = File.ReadAllText(scriptPath);
            var ast = ParseFile(source, scriptPath);
            if (ast is not ProgramExpr program) return [];

            var importMap = BuildImportMap(program);

            // 1. 找 return { ... }
            Expr? targetValue = null;
            string? targetVarName = null;
            foreach (var stmt in program.Statements)
            {
                if (stmt is ReturnExpr ret && ret.Value is ObjectLiteralExpr obj)
                {
                    foreach (var prop in obj.Properties)
                    {
                        if (prop.Key == memberName)
                        {
                            targetValue = prop.Value;
                            break;
                        }
                    }
                    break;
                }
            }
            if (targetValue == null) return [];

            // 2. IdentifierExpr → 追踪定义
            if (targetValue is IdentifierExpr id)
            {
                targetVarName = id.Name;
                targetValue = FindDefinition(program, id.Name);
            }

            // 3. @type 注解（变量定义上）
            if (targetVarName != null)
            {
                var annotated = AnnotationParser.FindAnnotation(source, targetVarName);
                if (annotated is { Count: > 0 }) return annotated;
            }

            // 4. CallExpr → 跨文件 @returns 注解
            if (targetValue is CallExpr call && call.Target is IdentifierExpr callId)
            {
                var crossFile = ResolveCrossFileCall(callId.Name, importMap, program);
                if (crossFile is { Count: > 0 }) return crossFile;
            }

            // 5. AST 结构推断
            return InferMembers(targetValue);
        }
        catch { return []; }
    }

    private static List<ModuleMemberInfo>? ResolveCrossFileCall(string funcName, Dictionary<string, string> importMap, ProgramExpr currentProgram)
    {
        if (!importMap.TryGetValue(funcName, out var importPath)) return null;
        if (!importPath.EndsWith(".script", StringComparison.OrdinalIgnoreCase)) return null;

        string resolvedPath;
        string currentFilePath = currentProgram.SourceSpan.FilePath;
        if (Path.IsPathRooted(importPath))
            resolvedPath = Path.GetFullPath(importPath);
        else
        {
            var baseDir = Path.GetDirectoryName(Path.GetFullPath(currentFilePath));
            if (baseDir == null) return null;
            resolvedPath = Path.GetFullPath(Path.Combine(baseDir, importPath));
        }
        if (!File.Exists(resolvedPath)) return null;

        try
        {
            string targetSource = File.ReadAllText(resolvedPath);
            var annotated = AnnotationParser.FindAnnotation(targetSource, funcName);
            if (annotated is { Count: > 0 }) return annotated;

            // 递归追踪
            var targetAst = ParseFile(targetSource, resolvedPath);
            if (targetAst is ProgramExpr tp)
            {
                var targetImportMap = BuildImportMap(tp);
                var targetDef = FindDefinition(tp, funcName);
                if (targetDef is CallExpr nc && nc.Target is IdentifierExpr nid)
                    return ResolveCrossFileCall(nid.Name, targetImportMap, tp);
            }
        }
        catch { }
        return null;
    }

    private static Dictionary<string, string> BuildImportMap(ProgramExpr program)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var stmt in program.Statements)
            if (stmt is ImportStmt imp)
                foreach (var (m, a) in imp.Members)
                    map[a ?? m] = imp.FilePath;
        return map;
    }

    private static Expr? FindDefinition(ProgramExpr program, string name)
    {
        foreach (var stmt in program.Statements)
        {
            if (stmt is LetExpr l && l.Name == name) return l.Value;
            if (stmt is VarExpr v && v.Name == name) return v.Value;
        }
        return null;
    }

    private static List<ModuleMemberInfo> InferMembers(Expr? expr)
    {
        if (expr is ObjectLiteralExpr obj)
        {
            var members = new List<ModuleMemberInfo>();
            foreach (var prop in obj.Properties)
                members.Add(new ModuleMemberInfo { Name = prop.Key, IsProperty = prop.Value is not LambdaExpr, Description = prop.Value is LambdaExpr ? $"function {prop.Key}" : $"(property) {prop.Key}" });
            return members;
        }
        return [];
    }

    private static Expr? ParseFile(string source, string filePath)
    {
        try
        {
            var lexer = new ScriptLang.Lexer.Lexer(source, filePath);
            var parser = new ScriptLang.Parser.Parser(lexer.ScanTokens(), filePath);
            return parser.Parse();
        }
        catch { return null; }
    }

    // ==================== 类型推断 + 原型成员 ====================

    private static readonly Dictionary<string, List<ModuleMemberInfo>> _prototypeMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = PMs(("length",true,"int"),("toUpper",false,"String toUpper()"),("toLower",false,"String toLower()"),("trim",false,"String trim()"),("split",false,"Array split(separator)"),("substring",false,"String substring(start,len?)"),("contains",false,"Bool contains(value)")),
        ["array"]  = PMs(("count",true,"int"),("length",true,"int"),("add",false,"void add(item)"),("first",false,"any first()"),("last",false,"any last()"),("select",false,"Array select(fn)"),("where",false,"Array where(fn)"),("orderBy",false,"Array orderBy()"),("orderByDesc",false,"Array orderByDesc()"),("toList",false,"Array toList()")),
        ["object"]   = PMs(("count",true,"int"),("keys",false,"Array keys()"),("values",false,"Array values()"),("has",false,"Bool has(key)"),("get",false,"any get(key)"),("set",false,"void set(key,value)"),("containsKey",false,"Bool containsKey(key)"),("remove",false,"Bool remove(key)"),("clear",false,"void clear()")),
        ["datetime"] = PMs(("year",true,"int"),("month",true,"int"),("day",true,"int"),("hour",true,"int"),("minute",true,"int"),("second",true,"int"),("millisecond",true,"int"),("ticks",true,"long"),("dayOfWeek",true,"int"),("dayOfYear",true,"int"),("toString",false,"String toString(format?)")),
        ["timespan"] = PMs(("days",true,"int"),("hours",true,"int"),("minutes",true,"int"),("seconds",true,"int"),("milliseconds",true,"int"),("totalDays",true,"double"),("totalHours",true,"double"),("totalMinutes",true,"double"),("totalSeconds",true,"double"),("totalMilliseconds",true,"double"),("ticks",true,"long"),("toString",false,"String toString(format?)")),
    };
    private static List<ModuleMemberInfo> PMs(params (string name, bool isProp, string desc)[] items) => items.Select(i => new ModuleMemberInfo { Name = i.name, IsProperty = i.isProp, Description = i.desc }).ToList();

    public static List<ModuleMemberInfo> GetPrototypeMembers(string typeName) => _prototypeMembers.TryGetValue(typeName, out var m) ? m : [];

    public static string InferExprType(Expr? expr) => expr switch
    {
        LiteralExpr lit => lit.Value switch { string => "string", int or long or float or double or decimal => "number", bool => "bool", _ => "unknown" },
        ArrayLiteralExpr => "array",
        ObjectLiteralExpr => "object",
        LambdaExpr => "function",
        CallExpr call when call.Target is IdentifierExpr id => id.Name switch
        {
            "now" or "date" => "datetime",
            "timespan" => "timespan",
            _ => "unknown"
        },
        _ => "unknown"
    };

    public static Expr? FindVariableInit(ProgramExpr program, string varName)
    {
        foreach (var stmt in program.Statements)
        {
            if (stmt is LetExpr l && l.Name == varName) return l.Value;
            if (stmt is VarExpr v && v.Name == varName) return v.Value;
        }
        return null;
    }
}
