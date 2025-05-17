using System.Text.Json.Serialization;

public class AnswerModel
{
    public string apikey { get; set; }
    public string description { get; set; }
    public string copyright { get; set; }

    [JsonPropertyName("test-data")]
    public List<TestItem> TestData { get; set; }
}

public class FinalPayload
{
    public string task { get; set; }
    public string apikey { get; set; }
    public AnswerModel answer { get; set; }
}
