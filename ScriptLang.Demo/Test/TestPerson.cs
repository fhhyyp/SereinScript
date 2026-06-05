using ScriptLang.Parser;
using ScriptLang.Runtime;

namespace ScriptLang.Demo;

/// <summary>
/// 测试用的 CLR 类
/// </summary>
[ScirptObject]
public class TestPerson
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public List<string> Hobbies { get; set; } = [];
    public string Greet() => $"Hello, I'm {Name}!";
    public int AddYears(int years) => Age + years;
    public void AddHobbies(string hobbie) => Hobbies.Add(hobbie);
    public void SetName(string name) => Name = name;
    public static string GetSpecies() => "Human";
}

[AttributeUsage(AttributeTargets.Class)]
internal class ScirptObjectAttribute : Attribute
{
}