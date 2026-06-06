using System.Security.Cryptography;
using System.Text;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    /// <summary>
    /// 加密模块
    /// </summary>   
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class CryptoModule : ScriptRuntimeObject<CryptoModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is CryptoModule;

        /// <summary>
        /// 计算哈希值
        /// </summary>
        /// <param name="algorithm">哈希算法（md5, sha1, sha256, sha512）</param>
        /// <param name="data">输入数据</param>
        /// <returns>哈希值（十六进制字符串）</returns>
        [PrototypeFunction]
        public static StringValue Hash(StringValue algorithm, StringValue data)
        {
            using var hash = HashAlgorithm.Create(algorithm.Value);
            if (hash == null)
            {
                throw new ArgumentException($"不支持的哈希算法: {algorithm.Value}");
            }

            var bytes = Encoding.UTF8.GetBytes(data.Value);
            var hashBytes = hash.ComputeHash(bytes);
            var hashString = Convert.ToHexString(hashBytes).ToLower();
            return StringValue.Create(hashString);
        }

        /// <summary>
        /// 生成随机字节
        /// </summary>
        /// <param name="length">字节长度</param>
        /// <returns>Base64 编码的随机字符串</returns>
        [PrototypeFunction]
        public static StringValue RandomBytes(NumberValue<int> length)
        {
            var bytes = RandomNumberGenerator.GetBytes(length.Value);
            return StringValue.Create(Convert.ToBase64String(bytes));
        }

        /// <summary>
        /// 生成随机字符串
        /// </summary>
        /// <param name="length">字符串长度</param>
        /// <returns>随机字符串</returns>
        [PrototypeFunction]
        public static StringValue RandomString(NumberValue<int> length)
        {
            const string chars = "dsf908234jkdsf23984fdsny3sady9321h4";
            var bytes = RandomNumberGenerator.GetBytes(length.Value);
            var result = new char[length.Value];

            for (int i = 0; i < length.Value; i++)
            {
                result[i] = chars[bytes[i] % chars.Length];
            }

            return StringValue.Create(new string(result));
        }

        /// <summary>
        /// 生成 UUID
        /// </summary>
        /// <returns>UUID 字符串</returns>
        [PrototypeFunction]
        public static StringValue Uuid() => StringValue.Create(Guid.NewGuid().ToString());

        /// <summary>
        /// 支持的哈希算法列表
        /// </summary>
        [PrototypeProperty]
        private static ObjectValue Algorithms()
        {
            return new ObjectValue(new Dictionary<string, Value>
            {
                ["md5"] = StringValue.Create("MD5"),
                ["sha1"] = StringValue.Create("SHA1"),
                ["sha256"] = StringValue.Create("SHA256"),
                ["sha512"] = StringValue.Create("SHA512")
            });
        }

        /// <summary>
        /// 计算 HMAC
        /// </summary>
        /// <param name="algorithm">哈希算法</param>
        /// <param name="data">输入数据</param>
        /// <param name="key">密钥</param>
        /// <returns>HMAC 值</returns>
        [PrototypeFunction]
        public static StringValue Hmac(StringValue algorithm, StringValue data, StringValue key)
        {
            var algorithmName = algorithm.Value switch
            {
                "md5" => "HMACMD5",
                "sha1" => "HMACSHA1",
                "sha256" => "HMACSHA256",
                "sha512" => "HMACSHA512",
                _ => throw new ArgumentException($"不支持的 HMAC 算法: {algorithm.Value}")
            };

            using var hmac = HMAC.Create(algorithmName);
            if (hmac == null)
            {
                throw new ArgumentException($"无法创建 HMAC 实例: {algorithmName}");
            }

            hmac.Key = Encoding.UTF8.GetBytes(key.Value);
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data.Value));
            var hashString = Convert.ToHexString(hashBytes).ToLower();
            return StringValue.Create(hashString);
        }
    }
}