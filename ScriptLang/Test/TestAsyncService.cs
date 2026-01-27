namespace ScriptLang;

/// <summary>
/// 测试异步 CLR 方法
/// </summary>
public class TestAsyncService
{
    public enum State
    {
        On,
        Off
    }

    public async Task<State> GetStateAsync()
    {
        await Task.Delay(1000);
        return State.Off;
    }

    public async Task<string> FetchDataAsync(string url)
    {
        await Task.Delay(100);
        return $"Data from {url}";
    }

    public async Task<int?> ComputeAsync(int x, int y)
    {
        await Task.Delay(500);
        return x * y + 10;
    }

    public async Task<List<string>> GetItemsAsync()
    {
        await Task.Delay(1000);
        return new List<string> { "Item1", "Item2", "Item3" };
    }

    public static async Task<string> StaticAsyncMethod(string message)
    {
        await Task.Delay(500);
        return $"Static: {message}";
    }
}
