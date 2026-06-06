using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    /// <summary>
    /// 网络模块
    /// </summary>
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class NetworkModule : ScriptRuntimeObject<NetworkModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is NetworkModule;

        /// <summary>
        /// 发送 HTTP GET 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="options">请求选项（headers, timeout 等）</param>
        /// <returns>响应对象</returns>
        [PrototypeFunction]
        public static async Task<ObjectValue> HttpGet(StringValue url, ObjectValue options)
        {
            using var client = new HttpClient();

            if (options != null && options.TryGetValue("headers", out var headers))
            {
                var headersObj = headers.AsObject();
                foreach (var h in headersObj)
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(h.Key, h.Value.AsString());
                }
            }

            var response = await client.GetAsync(url.Value);
            var content = await response.Content.ReadAsStringAsync();

            var result = new ObjectValue(new Dictionary<string, Value>
            {
                ["status"] = NumberValueFactory.Create((int)response.StatusCode),
                ["statusText"] = StringValue.Create(response.ReasonPhrase ?? ""),
                ["headers"] = HttpHeadersToObject(response.Headers),
                ["data"] = StringValue.Create(content)
            });

            return result;
        }

        /// <summary>
        /// 发送 HTTP POST 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="data">请求数据</param>
        /// <param name="contentType">内容类型（默认 application/json）</param>
        /// <returns>响应对象</returns>
        [PrototypeFunction]
        public static async Task<ObjectValue> HttpPost(StringValue url, Value data, StringValue contentType)
        {
            using var client = new HttpClient();
            HttpContent content = null;

            if (data != null)
            {
                var json = data.IsString ? data.AsString() : JsonSerializer.Serialize(data);
                content = new StringContent(json, Encoding.UTF8, contentType?.Value ?? "application/json");
            }

            var response = await client.PostAsync(url.Value, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            return new ObjectValue(new Dictionary<string, Value>
            {
                ["status"] = NumberValueFactory.Create((int)response.StatusCode),
                ["statusText"] = StringValue.Create(response.ReasonPhrase ?? ""),
                ["data"] = StringValue.Create(responseContent)
            });
        }

        /// <summary>
        /// 创建 TCP 客户端
        /// </summary>
        /// <returns>TCP 客户端对象</returns>
        //[PrototypeFunction]
        public static ClrObjectValue CreateTcpClient() => new ClrObjectValue(new TcpClientWrapper());

        /// <summary>
        /// 创建 WebSocket 客户端
        /// </summary>
        /// <param name="url">WebSocket URL</param>
        /// <returns>WebSocket 客户端对象</returns>
        //[PrototypeFunction]
        public static ClrObjectValue CreateWebSocket(StringValue url) => new ClrObjectValue(new WebSocketWrapper(url.Value));

        /// <summary>
        /// 将 HTTP 响应头转换为对象
        /// </summary>
        private static ObjectValue HttpHeadersToObject(HttpResponseHeaders headers)
        {
            var dict = new Dictionary<string, Value>();
            foreach (var header in headers)
            {
                dict[header.Key] = StringValue.Create(string.Join(", ", header.Value));
            }
            return new ObjectValue(dict);
        }
    }

    /// <summary>
    /// TCP 客户端包装器
    /// </summary>
    public class TcpClientWrapper : IDisposable
    {
        private TcpClient? _client;
        private NetworkStream? _stream;

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="host">主机地址</param>
        /// <param name="port">端口号</param>
        public async Task ConnectAsync(string host, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="length">读取字节数（默认 1024）</param>
        /// <returns>读取的字符串</returns>
        public async Task<string> ReadAsync(int length = 1024)
        {
            var buffer = new byte[length];
            var bytesRead = await _stream!.ReadAsync(buffer, 0, length);
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="data">要写入的字符串</param>
        public async Task WriteAsync(string data)
        {
            var buffer = Encoding.UTF8.GetBytes(data);
            await _stream!.WriteAsync(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            _stream?.Close();
            _client?.Close();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose() => Close();
    }

    /// <summary>
    /// WebSocket 包装器
    /// </summary>
    public class WebSocketWrapper : IDisposable
    {
        private ClientWebSocket? _webSocket;

        /// <summary>
        /// 初始化 WebSocket 包装器
        /// </summary>
        /// <param name="url">WebSocket URL</param>
        public WebSocketWrapper(string url)
        {
            _webSocket = new ClientWebSocket();
            _webSocket.ConnectAsync(new Uri(url), CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="message">消息内容</param>
        public async Task SendAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(buffer);
            await _webSocket!.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        /// <summary>
        /// 接收消息
        /// </summary>
        /// <returns>接收到的消息</returns>
        public async Task<string> ReceiveAsync()
        {
            var buffer = new byte[1024];
            var segment = new ArraySegment<byte>(buffer);
            var result = await _webSocket!.ReceiveAsync(segment, CancellationToken.None);
            return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public async Task CloseAsync()
        {
            await _webSocket!.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _webSocket?.Dispose();
        }
    }
}