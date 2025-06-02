public class FactModel
{
    public string Content { get; set; }
    public List<string> Keywords { get; set; } = new();
    public List<string> Persons { get; set; } = new();
    public List<string> Professions { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public List<string> Places { get; set; } = new();
}