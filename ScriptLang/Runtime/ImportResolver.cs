using System.Collections.Concurrent;

namespace ScriptLang.Runtime;


/// <summary>
/// 模块解析器：负责执行模块
/// </summary>
public class ImportResolver
{
    private readonly ScriptEngine _engine;

    private readonly ConcurrentDictionary<string, ObjectValue> _moduleCache;

    public string RootPath { get; internal set; } = string.Empty;

    //private readonly string _baseDirectory;

    public ImportResolver(ScriptEngine engine/*, string baseDirectory*/)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        
        _moduleCache = new ConcurrentDictionary<string, ObjectValue>(StringComparer.OrdinalIgnoreCase);
        //_baseDirectory = baseDirectory;
    }

    /// <summary>
    /// 解析并导入模块
    /// </summary>
    public async Task<ObjectValue> ResolveAsync(string filePath, Scope? scope = null)
    {
        var fullPath = ResolveFilePath(filePath);

        if (_moduleCache.TryGetValue(fullPath, out var cached))
            return cached;
        
        if (!File.Exists(fullPath))
            throw new RuntimeException($"模块不存在: {fullPath}");

        Console.WriteLine($"解析模块 ： {filePath}");
        var re_scope = scope is null ? new Scope() : new Scope(scope);
        var result = await _engine.RunModuleAsync(fullPath, re_scope);
        var exports = ExtractExports(result);
        _moduleCache[fullPath] =  exports;
        re_scope.Clear();
        return exports;
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

    /// <summary>
    /// 获取模块导出成员
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    private static ObjectValue ExtractExports(Value result)
    {
        if (result is ObjectValue obj)
            return obj;

        return new ObjectValue([]);
    }

    /// <summary>
    /// 清除模块缓存
    /// </summary>
    public void ClearCache()
    {
        _moduleCache.Clear();
    }

    /// <summary>
    /// 清除指定模块的缓存
    /// </summary>
    public void ClearCache(string filePath)
    {
        var fullPath = ResolveFilePath(filePath);
        _moduleCache.TryRemove(fullPath, out _);
    }

    
}
