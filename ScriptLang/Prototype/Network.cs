using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using ScriptLang.Runtime;

namespace ScriptLang.Prototype
{
    [PrototypeExtension]
    internal sealed partial class Network : ScriptRuntimeObject<Network>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is Network;

        [PrototypeFunction]
        public static async Task<Value> HttpGet(StringValue url)
        {
            using var client = new HttpClient();
            try
            {
                var response = await client.GetAsync(url.Value);
                var content = await response.Content.ReadAsStringAsync();
                return new StringValue(content);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        [PrototypeFunction]
        public static async Task<Value> HttpPost(StringValue url, Value data)
        {
            using var client = new HttpClient();
            var content = new StringContent(data.AsString());
            var response = await client.PostAsync(url.Value, content);
            var result = await response.Content.ReadAsStringAsync();
            return new StringValue(result);
        }

        [PrototypeFunction]
        public static async Task<Value> HttpRequest(ObjectValue options)
        {
            using var client = new HttpClient();
            var method = options.Get("method").AsString().ToUpper();
            var url = options.Get("url").AsString();

            var request = new HttpRequestMessage(new HttpMethod(method), url);

            if (options.TryGetValue("headers", out var headers))
            {
                foreach (var kv in headers.AsObject())
                {
                    request.Headers.TryAddWithoutValidation(kv.Key, kv.Value.AsString());
                }
            }

            if (options.TryGetValue("body", out var body))
            {
                request.Content = new StringContent(body.AsString());
            }

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            var result = new ObjectValue(new Dictionary<string, Value>());
            result.Set("status", NumberValueFactory.Create((int)response.StatusCode));
            result.Set("body", new StringValue(responseBody));

            return result;
        }
    }

    // TCP 客户端
    [PrototypeExtension]
    internal sealed partial class TcpClient : ScriptRuntimeObject<TcpClient>
    {
        private System.Net.Sockets.TcpClient? _client;
        private NetworkStream? _stream;

        [PrototypeFunction]
        public async Task<Value> Connect(StringValue host, NumberValue<int> port)
        {
            _client = new System.Net.Sockets.TcpClient();
            await _client.ConnectAsync(host.Value, port.Value);
            _stream = _client.GetStream();
            return Value.Null;
        }

        [PrototypeFunction]
        public async Task<Value> Send(StringValue data)
        {
            var bytes = Encoding.UTF8.GetBytes(data.Value);
            await _stream!.WriteAsync(bytes);
            return NumberValueFactory.Create(bytes.Length);
        }

        [PrototypeFunction]
        public async Task<Value> Receive(NumberValue<int> length)
        {
            var buffer = new byte[length.Value];
            var received = await _stream!.ReadAsync(buffer);
            var data = Encoding.UTF8.GetString(buffer, 0, received);
            return new StringValue(data);
        }

        [PrototypeFunction]
        public void Close()
        {
            _stream?.Close();
            _client?.Close();
        }
    }
}
