using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
public class OpenAIService
{
    private readonly HttpClient? _client;
    private readonly string? _model;
    private readonly string? _defaultSystemPrompt;

    public OpenAIService()
    {

        _model = ConfigHelper.GetValue<string>("OpenAI:MODEL");
        _defaultSystemPrompt = ConfigHelper.GetValue<string>("OpenAI:DEFAULT_SYSTEM_PROMPT");

        _client = new HttpClient
        {
            BaseAddress = new Uri(ConfigHelper.GetValue<string>("OpenAI:URL"))
        };
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ConfigHelper.GetValue<string>("OpenAI:KEY"));


    }

    public async Task<string> GetAnswerAsync(string? question = null, string? systemPrompt = null)
    {
        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt ?? _defaultSystemPrompt },
                new { role = "user", content = question }
            },
            temperature = 0.7
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        if (_client != null)
        {
            var response = await _client.PostAsync("chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var parsed = JsonDocument.Parse(responseBody);

            return parsed.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()
                ?? "Brak odpowiedzi";
        }
        return GetType().Name + "GetAnswerAsync Error";
    }
}