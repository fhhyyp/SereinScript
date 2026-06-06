using ScriptLang.Runtime.ByteCode;
using System.Collections.Concurrent;

namespace ScriptLang.Runtime;

/// <summary>
/// 模块解析器：负责加载并执行被 import 的模块
/// 支持 .script（源码）和 .ssc（编译产物）两种格式，优先加载 .ssc
/// </summary>
public class ImportResolver(ScriptEngine engine)
{
    private readonly ScriptEngine _engine = engine ?? throw new ArgumentNullException(nameof(engine));

    /// <summary>模块缓存（按解析后的完整路径去重）</summary>
    private readonly ConcurrentDictionary<string, ObjectValue> _moduleCache = new(StringComparer.OrdinalIgnoreCase);

    public string RootPath { get; internal set; } = string.Empty;

    /// <summary>
    /// 解析并导入模块
    /// </summary>
    /// <param name="filePath">import 语句中的文件路径（如 "test-import.script"）</param>
    /// <param name="scope">外部作用域</param>
    public async Task<ObjectValue> ResolveAsync(string filePath, Scope? scope = null)
    {
        var fullPath = ResolveImportPath(filePath);

        if (_moduleCache.TryGetValue(fullPath, out var cached))
            return cached;

        if (fullPath.EndsWith(".ssc", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadCompiledModuleAsync(fullPath);
        }
        else
        {
            return await LoadSourceModuleAsync(fullPath, scope);
        }
    }

    /// <summary>
    /// 解析 import 路径，按优先级查找实际文件：
    ///   1. 显式 .ssc        → 直接使用，回退 .script
    ///   2. 显式 .script     → 优先 .ssc，回退 .script
    ///   3. 无后缀           → 优先 .ssc，回退 .script
    /// </summary>
    private string ResolveImportPath(string filePath)
    {
        string basePath = ResolveFilePath(filePath);

        // 已明确指定 .ssc
        if (basePath.EndsWith(".ssc", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(basePath)) return basePath;
            string scriptFallback = Path.ChangeExtension(basePath, ".script");
            if (File.Exists(scriptFallback)) return scriptFallback;
            throw new RuntimeException($"模块不存在: {filePath}（查找了 {basePath} 和 {scriptFallback}）");
        }

        // 已明确指定 .script 或其他后缀
        if (Path.HasExtension(basePath))
        {
            // 优先 .ssc
            string sscPath = Path.ChangeExtension(basePath, ".ssc");
            if (File.Exists(sscPath)) return sscPath;
            // 回退原路径
            if (File.Exists(basePath)) return basePath;
            throw new RuntimeException($"模块不存在: {filePath}（查找了 {sscPath} 和 {basePath}）");
        }

        // 无后缀：优先 .ssc，回退 .script
        string sscPath2 = basePath + ".ssc";
        if (File.Exists(sscPath2)) return sscPath2;
        string scriptPath = basePath + ".script";
        if (File.Exists(scriptPath)) return scriptPath;
        throw new RuntimeException($"模块不存在: {filePath}（查找了 {sscPath2} 和 {scriptPath}）");
    }

    /// <summary>
    /// 解析文件路径（支持相对路径）
    /// </summary>
    private string ResolveFilePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
            return Path.GetFullPath(filePath);

        return Path.GetFullPath(Path.Combine(RootPath, filePath));
    }

    /// <summary>加载编译后的 .ssc 模块</summary>
    private async Task<ObjectValue> LoadCompiledModuleAsync(string sscPath)
    {
        var chunk = ByteCodeChunk.Load(sscPath);

        // 通过 ScriptEngine.CreateTask 执行，自动处理 ImportResolver.RootPath 和 GlobalSlotRegistry
        var task = _engine.CreateTask(chunk, sscPath);
        var result = await task.RunAsync();
        var exports = ExtractExports(result);
        _moduleCache[sscPath] = exports;
        return exports;
    }

    /// <summary>加载源码 .script 模块（原有逻辑）</summary>
    private async Task<ObjectValue> LoadSourceModuleAsync(string fullPath, Scope? scope)
    {
        if (!File.Exists(fullPath))
            throw new RuntimeException($"模块不存在: {fullPath}");

        Console.WriteLine($"解析模块 ： {Path.GetFileName(fullPath)}");
        var re_scope = scope is null ? new Scope() : new Scope(scope);
        var result = await _engine.RunModuleAsync(fullPath, re_scope);
        var exports = ExtractExports(result);
        _moduleCache[fullPath] = exports;
        re_scope.Clear();
        return exports;
    }

    /// <summary>从执行结果提取模块导出成员</summary>
    private static ObjectValue ExtractExports(Value result)
    {
        if (result is ObjectValue obj)
            return obj;

        return new ObjectValue([]);
    }

    /// <summary>清除模块缓存</summary>
    public void ClearCache()
    {
        _moduleCache.Clear();
    }

    /// <summary>清除指定模块的缓存</summary>
    public void ClearCache(string filePath)
    {
        var fullPath = ResolveFilePath(filePath);
        _moduleCache.TryRemove(fullPath, out _);
    }
}
