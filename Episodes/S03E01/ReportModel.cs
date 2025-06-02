public class ReportModel
{
    public string FileName { get; set; }
    public string Content { get; set; }
    public List<string> Keywords { get; set; } = new();
    public List<string> Persons { get; set; } = new();
    public List<string> Places { get; set; } = new();
    public List<string> Events { get; set; } = new(); // Nowe pole dla zdarzeń
}