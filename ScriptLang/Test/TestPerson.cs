namespace ScriptLang;

/// <summary>
/// 测试用的 CLR 类
/// </summary>
public class TestPerson
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public List<string> Hobbies { get; set; } = new();

    public string Greet() => $"Hello, I'm {Name}!";
    public int AddYears(int years) => Age + years;
    public void SetName(string name) => Name = name;
    public static string GetSpecies() => "Human";
}
