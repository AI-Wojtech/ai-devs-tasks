using System.Text.Json.Serialization;

public class OpenAiImagesVisionRequestModel
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = null!;

    [JsonPropertyName("input")]
    public List<Message> Input { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.2;
}

public class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = null!; // e.g. "user", "system"

    [JsonPropertyName("content")]
    public List<IContent> Content { get; set; } = new();
}

public interface IContent
{
    [JsonPropertyName("type")]
    public string Type { get; }
}

public class TextContent : IContent
{
    [JsonPropertyName("type")]
    public string Type => "input_text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;
}

public class ImageContent : IContent
{
    [JsonPropertyName("type")]
    public string Type => "input_image";

    [JsonPropertyName("image_url")]
    public ImageUrl ImageUrl { get; set; } = null!;
}

public class ImageUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;
}
