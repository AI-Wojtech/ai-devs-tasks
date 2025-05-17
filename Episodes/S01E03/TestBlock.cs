using System.Text.Json.Serialization;

public class TestBlock
{
    [JsonPropertyName("q")]
    public string Q { get; set; }

    [JsonPropertyName("a")]
    public string A { get; set; }
}