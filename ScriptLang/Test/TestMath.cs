namespace ScriptLang;

/// <summary>
/// 测试用的 CLR 工具类
/// </summary>
public class TestMath
{
    public async Task<int> AddAsync(int a, int b)
    {
        await Task.Delay(50);
        return a + b;
    }
    public static double Max(double a, double b) => Math.Max(a, b);
    public static double Min(double a, double b) => Math.Min(a, b);
    public static double Clamp(double value, double min, double max) => Math.Clamp(value, min, max);
}
