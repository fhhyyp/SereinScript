using ScriptLang.Runtime;

namespace ScriptLang.System
{
    /// <summary>
    /// 路径处理模块
    /// </summary>
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class PathModule : ScriptRuntimeObject<PathModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is PathModule;

        /// <summary>
        /// 连接路径片段
        /// </summary>
        /// <param name="paths">路径片段数组</param>
        /// <returns>连接后的路径</returns>
        [PrototypeFunction]
        public static StringValue Join(ArrayValue paths)
        {
            var pathList = paths.Elements.Select(p => p.AsString()).ToArray();
            return StringValue.Create(Path.Combine(pathList));
        }

        /// <summary>
        /// 解析为绝对路径
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>绝对路径</returns>
        [PrototypeFunction]
        public static StringValue Resolve(StringValue path)
            => StringValue.Create(Path.GetFullPath(path.Value));

        /// <summary>
        /// 获取目录名
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>目录名</returns>
        [PrototypeFunction]
        public static StringValue Dirname(StringValue path)
            => StringValue.Create(Path.GetDirectoryName(path.Value) ?? "");

        /// <summary>
        /// 获取文件名
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="ext">要移除的扩展名（可选）</param>
        /// <returns>文件名</returns>
        [PrototypeFunction]
        public static StringValue Basename(StringValue path, StringValue ext)
        {
            var fileName = Path.GetFileName(path.Value);
            if (ext != null && fileName.EndsWith(ext.Value))
            {
                fileName = fileName[..^ext.Value.Length];
            }
            return StringValue.Create(fileName);
        }

        /// <summary>
        /// 获取文件扩展名
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>扩展名（包含点号）</returns>
        [PrototypeFunction]
        public static StringValue Extname(StringValue path)
            => StringValue.Create(Path.GetExtension(path.Value));

        /// <summary>
        /// 目录分隔符
        /// </summary>
        [PrototypeProperty]
        private static StringValue Separator()
            => StringValue.Create(Path.DirectorySeparatorChar.ToString());

        /// <summary>
        /// 路径分隔符
        /// </summary>
        [PrototypeProperty]
        private static StringValue Delimiter()
            => StringValue.Create(Path.PathSeparator.ToString());

        /// <summary>
        /// 规范化路径
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>规范化后的路径</returns>
        [PrototypeFunction]
        public static StringValue Normalize(StringValue path)
            => StringValue.Create(Path.GetFullPath(path.Value));
    }
}