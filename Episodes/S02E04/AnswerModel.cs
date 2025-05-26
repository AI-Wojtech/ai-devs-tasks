using System.Collections.Generic;
using Newtonsoft.Json;

public class Payload
{
    [JsonProperty("task")]
    public string Task { get; set; }

    [JsonProperty("apikey")]
    public string ApiKey { get; set; }

    [JsonProperty("answer")]
    public Answer Answer { get; set; }
}

public class Answer
{
    [JsonProperty("people")]
    public List<string> People { get; set; }

    [JsonProperty("hardware")]
    public List<string> Hardware { get; set; }
}
