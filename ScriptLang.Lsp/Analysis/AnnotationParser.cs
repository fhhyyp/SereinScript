namespace ScriptLang.Lsp.Analysis;

/// <summary>
/// 解析 SereinScript JSDoc 风格注释：@returns {{ ... }} 和 @type {{ ... }}
/// </summary>
public static class AnnotationParser
{
    public static List<ModuleMemberInfo>? FindAnnotation(string source, string definitionName)
    {
        // 规范化换行：\r\n → \n
        source = source.Replace("\r\n", "\n").Replace('\r', '\n');

        // 查找 "let createStore" 或 "var createStore"
        int defIndex = -1;
        foreach (var p in new[] { $"let {definitionName}", $"var {definitionName}" })
        {
            defIndex = source.IndexOf(p, StringComparison.Ordinal);
            if (defIndex >= 0) break;
        }
        if (defIndex < 0) return null;

        return ScanAnnotationBefore(source, defIndex);
    }

    private static List<ModuleMemberInfo>? ScanAnnotationBefore(string source, int defIndex)
    {
        // 定位到定义行的前一个 \n，然后从该 \n 之前开始扫描
        int scanStart = defIndex;
        while (scanStart > 0 && source[scanStart - 1] != '\n') scanStart--;
        // scanStart = 定义行首字符。scanStart - 1 = 上一行末尾的 \n
        // pos 从 \n 的前一个字符开始（即上一行的最后一个字符）
        int pos = scanStart - 1;
        if (pos < 0) return null; // 定义在文件第一行，没注释
        pos--; // 跳过 \n，进入上一行

        var commentLines = new List<string>();
        while (pos >= 0)
        {
            int lineEnd = pos;
            while (pos >= 0 && source[pos] != '\n') pos--;
            int lineStart = pos + 1;
            int contentStart = lineStart;

            while (contentStart <= lineEnd && (source[contentStart] == ' ' || source[contentStart] == '\t'))
                contentStart++;

            if (contentStart + 1 <= lineEnd && source[contentStart] == '/' && source[contentStart + 1] == '/')
            {
                commentLines.Insert(0, source[contentStart..(lineEnd + 1)].Trim());
            }
            else break;

            if (pos < 0) break;
            pos--;
        }

        if (commentLines.Count == 0) return null;
        return ParseCommentBlock(commentLines);
    }

    private static List<ModuleMemberInfo>? ParseCommentBlock(List<string> commentLines)
    {
        var stripped = commentLines.Select(line =>
        {
            int i = line.IndexOf("//", StringComparison.Ordinal);
            return i >= 0 ? line[(i + 2)..].Trim() : line.Trim();
        }).ToList();

        var joined = string.Join("\n", stripped);
        int tagIndex = -1;
        foreach (var tag in new[] { "@returns {{", "@type {{" })
        {
            tagIndex = joined.IndexOf(tag, StringComparison.Ordinal);
            if (tagIndex >= 0) break;
        }
        if (tagIndex < 0) return null;

        int braceStart = joined.IndexOf("{{", tagIndex, StringComparison.Ordinal) + 2;
        int braceEnd = joined.IndexOf("}}", braceStart, StringComparison.Ordinal);
        if (braceStart < 2 || braceEnd < 0) return null;

        string content = joined[braceStart..braceEnd].Trim();
        // 诊断：打印原始内容（转义 \n 为可见形式）
        global::System.Console.Error.WriteLine($"[LSP.Annotation] Raw content between {{{{ }}}}: '{content.Replace("\n", "\\n").Replace("\r", "\\r")}'");
        var result = ParseMembers(content);
        global::System.Console.Error.WriteLine($"[LSP.Annotation] Found {result.Count} members: {string.Join(", ", result.Select(m => m.Name))}");
        return result;
    }

    private static List<ModuleMemberInfo> ParseMembers(string content)
    {
        var members = new List<ModuleMemberInfo>();
        // 先按换行分割，再按逗号分割（兼容两种格式）
        foreach (var line in content.Split('\n'))
        {
            foreach (var item in line.Split(','))
            {
                var trimmed = item.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                int colon = trimmed.IndexOf(':');
                if (colon > 0)
                    members.Add(new ModuleMemberInfo { Name = trimmed[..colon].Trim(), IsProperty = false, Description = trimmed[(colon + 1)..].Trim() });
                else
                    members.Add(new ModuleMemberInfo { Name = trimmed, IsProperty = false, Description = $"(annotated) {trimmed}" });
            }
        }
        return members;
    }
}
