using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

public class HttpService
{
    private readonly HttpClient _httpClient;
    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";
    public HttpService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<string> GetAsync(string url)
    {
        return await _httpClient.GetStringAsync(url);
    }
    public async Task<string> GetHtmlAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);

            // NIE WOLNO wywoływać EnsureSuccessStatusCode tutaj
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Błąd pobierania strony: {url} — status {response.StatusCode}");
                return string.Empty;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Wyjątek HttpRequestException dla URL {url}: {ex.Message}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Nieoczekiwany wyjątek dla URL {url}: {ex.Message}");
            return string.Empty;
        }
    }


    public async Task<T> GetJsonAsync<T>(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }

    public async Task SendJsonAsync(string url, object payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> PostAsync(string url, HttpContent content)
    {
        var response = await _httpClient.PostAsync(url, content);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<HttpResult<TResponse>> PostJsonAsync<TRequest, TResponse>(string url, TRequest payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
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
            result.Data = System.Text.Json.JsonSerializer.Deserialize<TResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (System.Text.Json.JsonException ex)
        {
            Console.WriteLine($"JSON deserialization failed: {ex.Message}");
        }

        return result;
    }

    public async Task<T> PostJsonAsync<T>(string url, object payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        Console.WriteLine($"Wysyłany JSON: {json}");

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Response: {responseContent}");

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Request failed with status {response.StatusCode}: {responseContent}");
        }

        var result = System.Text.Json.JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    public async Task<ReportApiResponse> SendAnswerAsync(object answer, string taskName)
    {
        var request = new ReportRequest
        {
            Task = taskName,
            Apikey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY"),
            Answer = answer
        };

        return await PostJsonAsync<ReportApiResponse>("https://c3ntrala.ag3nts.org/report", request);
    }

    public class ReportRequest
    {
        [JsonPropertyName("task")]
        public string Task { get; set; }

        [JsonPropertyName("apikey")]
        public string Apikey { get; set; }

        [JsonPropertyName("answer")]
        public object Answer { get; set; }
    }
}
