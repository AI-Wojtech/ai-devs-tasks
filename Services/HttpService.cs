using System.Net.Http;
using System.Text;
using System.Text.Json;

public class HttpService
{
    private readonly HttpClient _client;

    public HttpService()
    {
        _client = new HttpClient();
    }

    public async Task<string> GetAsync(string url)
    {
        return await _client.GetStringAsync(url);
    }

    public async Task<string> PostAsync(string url, HttpContent content)
    {
        var response = await _client.PostAsync(url, content);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<HttpResult<TResponse>> PostJsonAsync<TRequest, TResponse>(string url, TRequest payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        var result = new HttpResult<TResponse>
        {
            StatusCode = response.StatusCode,
            RawBody = responseBody
        };

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[HTTP {response.StatusCode}] Request to {url} failed.");
            return result;
        }

        try
        {
            result.Data = JsonSerializer.Deserialize<TResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON deserialization failed: {ex.Message}");
        }

        return result;
    }
}
