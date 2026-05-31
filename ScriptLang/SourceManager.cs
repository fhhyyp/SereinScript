using ScriptLang.Runtime;

namespace ScriptLang;

public sealed class SourceManager
{
    private readonly Dictionary<string, string> _sources = new();

    public void AddSource(string filePath, string source)
    {
        _sources[filePath] = source;
    }

    public string GetSource(string filePath)
    {
        if (!_sources.TryGetValue(filePath, out var src))
            throw new RuntimeException($"源文件未加载: {filePath}");
        return src;
    }

    public string GetSlice(string filePath, int start, int length)
    {
        var src = GetSource(filePath);
        return src.Substring(start, length);
    }
}
