using System.Security.Cryptography;
using System.Text;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class CryptoModule : ScriptRuntimeObject<CryptoModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is CryptoModule;

        [PrototypeFunction] [LspDoc("计算指定算法的哈希值（支持 md5/sha1/sha256/sha512）")]
        public static StringValue Hash(StringValue algorithm, StringValue data)
        { using var hash = HashAlgorithm.Create(algorithm.Value) ?? throw new ArgumentException($"不支持的哈希算法: {algorithm.Value}"); return StringValue.Create(Convert.ToHexString(hash.ComputeHash(Encoding.UTF8.GetBytes(data.Value))).ToLower()); }

        [PrototypeFunction] [LspDoc("计算 HMAC 消息认证码\n支持 md5/sha1/sha256/sha512")]
        public static StringValue Hmac(StringValue algorithm, StringValue data, StringValue key)
        { var algoName = algorithm.Value switch { "md5" => "HMACMD5", "sha1" => "HMACSHA1", "sha256" => "HMACSHA256", "sha512" => "HMACSHA512", _ => throw new ArgumentException($"不支持的 HMAC 算法: {algorithm.Value}") }; using var hmac = HMAC.Create(algoName)!; hmac.Key = Encoding.UTF8.GetBytes(key.Value); return StringValue.Create(Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data.Value))).ToLower()); }

        [PrototypeFunction] [LspDoc("生成指定长度的密码学安全随机字节（Base64 编码）")]
        public static StringValue RandomBytes(NumberValue<int> length) => StringValue.Create(Convert.ToBase64String(RandomNumberGenerator.GetBytes(length.Value)));

        [PrototypeFunction] [LspDoc("生成指定长度的随机字符串")]
        public static StringValue RandomString(NumberValue<int> length)
        { const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890"; var bytes = RandomNumberGenerator.GetBytes(length.Value); var result = new char[length.Value]; for (int i = 0; i < length.Value; i++) result[i] = chars[bytes[i] % chars.Length]; return StringValue.Create(new string(result)); }

        [PrototypeFunction] [LspDoc("生成 UUID v4 标识符")]
        public static StringValue Uuid() => StringValue.Create(Guid.NewGuid().ToString());

        [PrototypeProperty] [LspDoc("支持的哈希算法名称列表（md5/sha1/sha256/sha512）")]
        private static ObjectValue Algorithms() => new(new() { ["md5"] = StringValue.Create("MD5"), ["sha1"] = StringValue.Create("SHA1"), ["sha256"] = StringValue.Create("SHA256"), ["sha512"] = StringValue.Create("SHA512") });
    }
}
