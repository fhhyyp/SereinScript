using System.Text;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    /// <summary>
    /// 文件系统模块
    /// </summary>
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class FileModule : ScriptRuntimeObject<FileModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is FileModule;

        /// <summary>
        /// 同步读取文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="encoding">编码类型（默认 utf-8）</param>
        /// <returns>文件内容</returns>
        [PrototypeFunction]
        public static StringValue Read(StringValue path/*, StringValue encoding*/)
        {
            //var enc =  encoding?.Value ?? "utf-8"; Encoding.GetEncoding(enc)
            var content = File.ReadAllText(path.Value, Encoding.UTF8);
            return StringValue.Create(content);
        }

        /// <summary>
        /// 异步读取文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="encoding">编码类型（默认 utf-8）</param>
        /// <returns>文件内容</returns>
        [PrototypeFunction]
        public static async Task<StringValue> ReadAsync(StringValue path/*, StringValue encoding*/)
        {
            //var enc =  encoding?.Value ?? "utf-8"; Encoding.GetEncoding(enc)
            var content = await File.ReadAllTextAsync(path.Value, Encoding.UTF8);
            return StringValue.Create(content);
        }

        /// <summary>
        /// 同步写入文件
        /// </summary>
        /// <param name="path">目标路径</param>
        /// <param name="content">写入内容</param>
        /// <param name="encoding">编码类型（默认 utf-8）</param>
        [PrototypeFunction]
        public static void Write(StringValue path, StringValue content/*, StringValue encoding*/)
        {
            //var enc =  encoding?.Value ?? "utf-8"; Encoding.GetEncoding(enc)
            File.WriteAllText(path.Value, content.Value, Encoding.UTF8);
        }

        /// <summary>
        /// 异步写入文件
        /// </summary>
        /// <param name="path">目标路径</param>
        /// <param name="content">写入内容</param>
        /// <param name="encoding">编码类型（默认 utf-8）</param>
        [PrototypeFunction]
        public static async Task WriteAsync(StringValue path, StringValue content, StringValue encoding)
        {
            //var enc =  encoding?.Value ?? "utf-8"; Encoding.GetEncoding(enc)
            await File.WriteAllTextAsync(path.Value, content.Value, Encoding.UTF8);
        }

        /// <summary>
        /// 读取目录内容
        /// </summary>
        /// <param name="path">目录路径</param>
        /// <returns>文件和子目录名称数组</returns>
        [PrototypeFunction]
        public static ArrayValue ReadDir(StringValue path)
        {
            var files = Directory.GetFiles(path.Value);
            var dirs = Directory.GetDirectories(path.Value);
            var all = files.Concat(dirs).Select(f => (Value)StringValue.Create(f)).ToArray();
            return new ArrayValue([.. all]);
        }

        /// <summary>
        /// 判断文件或目录是否存在
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>是否存在</returns>
        [PrototypeFunction]
        public static BoolValue Exists(StringValue path) 
            => BoolValue.Create(File.Exists(path.Value) || Directory.Exists(path.Value));

        /// <summary>
        /// 创建目录
        /// </summary>
        /// <param name="path">创建路径（含目录名称）</param>
        [PrototypeFunction]
        public static void MkDir(StringValue path)
        {
            Directory.CreateDirectory(path.Value);
        }

        /// <summary>
        /// 删除文件或目录
        /// </summary>
        /// <param name="path">目标路径</param>
        [PrototypeFunction]
        public static void Remove(StringValue path)
        {
            if (File.Exists(path.Value))
                File.Delete(path.Value);
            else if (Directory.Exists(path.Value))
                Directory.Delete(path.Value, true);
        }

        /// <summary>
        /// 获取当前工作目录
        /// </summary>
        /// <returns>当前目录路径</returns>
        [PrototypeProperty]
        private static StringValue Cwd()
            => StringValue.Create(Directory.GetCurrentDirectory());

        /// <summary>
        /// 更改当前工作目录
        /// </summary>
        /// <param name="path">目标目录路径</param>
        [PrototypeFunction]
        public static void ChDir(StringValue path)
            => Directory.SetCurrentDirectory(path.Value);

        /// <summary>
        /// 获取文件状态信息
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>文件信息对象</returns>
        [PrototypeFunction]
        public static ObjectValue Stat(StringValue path)
        {
            var info = new FileInfo(path.Value);
            var obj = new ObjectValue(new Dictionary<string, Value>
            {
                ["size"] = NumberValueFactory.Create(info.Length),
                ["created"] = StringValue.Create(info.CreationTime.ToString("o")),
                ["modified"] = StringValue.Create(info.LastWriteTime.ToString("o")),
                ["isDirectory"] = BoolValue.Create((info.Attributes & FileAttributes.Directory) != 0),
                ["isFile"] = BoolValue.Create((info.Attributes & FileAttributes.Directory) == 0)
            });
            return obj;
        }
    }
}