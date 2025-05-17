using System.Text.Json.Serialization;

public class TestItem
{
    [JsonPropertyName("question")]
    public string Question { get; set; }

    [JsonPropertyName("answer")]
    public int Answer { get; set; }

    [JsonPropertyName("test")]
    public TestBlock Test { get; set; }
}