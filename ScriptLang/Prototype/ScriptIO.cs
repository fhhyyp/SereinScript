using System;
using System.Collections.Generic;
using System.Text;
using ScriptLang.Runtime;

namespace ScriptLang.Prototype
{

    [PrototypeExtension]
    internal sealed partial class FileSystem : ScriptRuntimeObject<FileSystem>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is FileSystem;

        [PrototypeFunction]
        public static Value ReadFile(StringValue path)
        {
            try
            {
                var content = File.ReadAllText(path.Value);
                return new StringValue(content);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        [PrototypeFunction]
        public static async Task<Value> ReadFileAsync(StringValue path)
        {
            try
            {
                var content = await File.ReadAllTextAsync(path.Value);
                return new StringValue(content);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        [PrototypeFunction]
        public static Value WriteFile(StringValue path, Value content)
        {
            try
            {
                File.WriteAllText(path.Value, content.AsString());
                return Value.Null;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        [PrototypeFunction]
        public static Value AppendFile(StringValue path, Value content)
        {
            File.AppendAllText(path.Value, content.AsString());
            return Value.Null;
        }

        [PrototypeFunction]
        public static Value Exists(StringValue path)
        {
            return BoolValue.Create(File.Exists(path.Value) || Directory.Exists(path.Value));
        }

        [PrototypeFunction]
        public static Value IsFile(StringValue path)
        {
            return BoolValue.Create(File.Exists(path.Value));
        }

        [PrototypeFunction]
        public static Value IsDirectory(StringValue path)
        {
            return BoolValue.Create(Directory.Exists(path.Value));
        }

        [PrototypeFunction]
        public static Value MkDir(StringValue path)
        {
            Directory.CreateDirectory(path.Value);
            return Value.Null;
        }

        [PrototypeFunction]
        public static Value ReadDir(StringValue path)
        {
            var entries = Directory.GetFileSystemEntries(path.Value);
            var array = new ArrayValue(new List<Value>());
            foreach (var entry in entries)
            {
                array.Add(new StringValue(entry));
            }
            return array;
        }

        [PrototypeFunction]
        public static Value Delete(StringValue path)
        {
            if (File.Exists(path.Value))
                File.Delete(path.Value);
            else if (Directory.Exists(path.Value))
                Directory.Delete(path.Value, true);
            return Value.Null;
        }

        [PrototypeFunction]
        public static Value Copy(StringValue source, StringValue dest)
        {
            File.Copy(source.Value, dest.Value, true);
            return Value.Null;
        }

        [PrototypeFunction]
        public static Value Move(StringValue source, StringValue dest)
        {
            File.Move(source.Value, dest.Value);
            return Value.Null;
        }

        [PrototypeFunction]
        public static Value Stat(StringValue path)
        {
            var info = new FileInfo(path.Value);
            var stat = new ObjectValue(new Dictionary<string, Value>());
            stat.Set("size", NumberValueFactory.Create(info.Length));
            stat.Set("created", new StringValue(info.CreationTime.ToString()));
            stat.Set("modified", new StringValue(info.LastWriteTime.ToString()));
            stat.Set("isDirectory", BoolValue.Create(Directory.Exists(path.Value)));
            return stat;
        }
    }

}


