using System.Text.Json.Serialization;

public class CalibrationRoot
{
    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("copyright")]
    public string Copyright { get; set; }

    [JsonPropertyName("test-data")]
    public List<TestItem> TestData { get; set; }
}