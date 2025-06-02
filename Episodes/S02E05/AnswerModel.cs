using System.Collections.Generic;
using Newtonsoft.Json;

public class Payload10
{
    [JsonProperty("task")]
    public string Task { get; set; }

    [JsonProperty("apikey")]
    public string ApiKey { get; set; }

    [JsonProperty("answer")]
    public Answer10 Answer { get; set; }
}
public class Answer10
{
    [JsonExtensionData]
    public Dictionary<string, object> Answers { get; set; } = new();
}