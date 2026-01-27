using ScriptLang.Parser;
using System.Collections.Concurrent;

namespace ScriptLang.Runtime;


/// <summary>
/// 模块解析器：负责执行模块
/// </summary>
public class ImportResolver
{
    private readonly ScriptEngine _engine;
    private readonly ConcurrentDictionary<string, ObjectValue> _moduleCache;
    private readonly string _baseDirectory;

    public ImportResolver(ScriptEngine engine, string baseDirectory)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        
        _moduleCache = new ConcurrentDictionary<string, ObjectValue>(StringComparer.OrdinalIgnoreCase);
        _baseDirectory = baseDirectory;
    }

    /// <summary>
    /// 解析并导入模块
    /// </summary>
    public async Task<ObjectValue> ResolveAsync(ImportStmt importStmt)
    {
        var fullPath = ResolveFilePath(importStmt.FilePath);

        if (_moduleCache.TryGetValue(fullPath, out var cached))
            return cached;

        if (!File.Exists(fullPath))
            throw new RuntimeException($"Module file not found: {fullPath}");

        var source = await File.ReadAllTextAsync(fullPath);

        // 统一入口：只允许走 engine
        var result = await _engine.RunAsync(source, fullPath);

        var exports = ExtractExports(result);

        _moduleCache.TryAdd(fullPath, exports);
        return exports;
    }


    /// <summary>
    /// 解析文件路径（支持相对路径）
    /// </summary>
    private string ResolveFilePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
            return Path.GetFullPath(filePath);

        return Path.GetFullPath(Path.Combine(_baseDirectory, filePath));
    }

    /// <summary>
    /// 获取模块导出成员
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    private ObjectValue ExtractExports(Value result)
    {
        if (result is ObjectValue obj)
            return obj;

        return new ObjectValue(new Dictionary<string, Value>());
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


/*/// <summary>
/// 模块解析器：负责加载、执行和缓存模块
/// </summary>
public class ImportResolver
{
    private readonly Interpreter _interpreter;
    private readonly ConcurrentDictionary<string, Value> _moduleCache;
    private readonly string? _baseDirectory;



    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="interpreter">解释器实例</param>
    /// <param name="baseDirectory">基础目录（用于解析相对路径），默认为当前工作目录</param>
    public ImportResolver(Interpreter interpreter, string? baseDirectory = null)
    {
        _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
        _moduleCache = new ConcurrentDictionary<string, Value>(StringComparer.OrdinalIgnoreCase);
        _baseDirectory = baseDirectory ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// 解析并导入模块
    /// </summary>
    /// <param name="importStmt">Import 语句</param>
    /// <param name="currentScope">当前作用域</param>
    /// <returns>模块 exports 对象</returns>
    public async Task<ObjectValue> ResolveAsync(ImportStmt importStmt, Scope currentScope)
    {
        // 1. 解析文件路径
        var fullPath = ResolveFilePath(importStmt.FilePath);

        // 2. 检查缓存
        if (_moduleCache.TryGetValue(fullPath, out var cachedExports))
        {
            return cachedExports as ObjectValue ?? throw new RuntimeException($"Cached module '{fullPath}' did not return an object");
        }

        // 3. 读取并解析模块文件
        var source = await ReadModuleSourceAsync(fullPath);
        var moduleAst = ParseModule(source);

        // 4. 创建独立作用域执行模块
        var moduleScope = new Scope(null); // 全局作用域作为根

        // 注入内置函数（如果需要）
        InjectBuiltIns(moduleScope);

        // 执行模块
        var result = await _interpreter.EvaluateAsync(moduleAst, moduleScope);

        // 5. 提取 exports（模块应返回对象字面量）
        var exports = ExtractExports(result.Value);

        // 6. 缓存模块
        _moduleCache.TryAdd(fullPath, exports);

        return exports;
    }

    /// <summary>
    /// 解析文件路径（支持相对路径）
    /// </summary>
    private string ResolveFilePath(string filePath)
    {
        // 处理绝对路径
        if (Path.IsPathRooted(filePath))
        {
            return Path.GetFullPath(filePath);
        }

        // 处理相对路径
        var combined = Path.Combine(_baseDirectory!, filePath);
        return Path.GetFullPath(combined);
    }

    /// <summary>
    /// 读取模块源代码
    /// </summary>
    private async Task<string> ReadModuleSourceAsync(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new RuntimeException($"Module file not found: {fullPath}");
        }

        try
        {
            return await File.ReadAllTextAsync(fullPath);
        }
        catch (Exception ex)
        {
            throw new RuntimeException($"Failed to read module file '{fullPath}': {ex.Message}");
        }
    }

    /// <summary>
    /// 解析模块源代码
    /// </summary>
    private Expr ParseModule(string source)
    {
        try
        {
            var lexer = new Lexer.Lexer(source);
            var tokens = lexer.ScanTokens();
            var parser = new Parser.Parser(filePath: tokens);
            return parser.Parse();
        }
        catch (Exception ex)
        {
            throw new RuntimeException($"Failed to parse module: {ex.Message}");
        }
    }

    /// <summary>
    /// 提取模块 exports
    /// </summary>
    private ObjectValue ExtractExports(Value result)
    {
        if (result is ObjectValue obj)
        {
            return obj;
        }

        // 如果模块返回 null 或其他类型，返回空对象
        return new ObjectValue(new Dictionary<string, Value>());
    }

    /// <summary>
    /// 注入内置函数到模块作用域
    /// </summary>
    private void InjectBuiltIns(Scope moduleScope)
    {
        BuiltinFunctions.RegisterAll(moduleScope);
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
*/