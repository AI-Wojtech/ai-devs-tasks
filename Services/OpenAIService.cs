using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public async Task<string> GetAnswerAsync(string? question = null, string? systemPrompt = null, string model = "")
    {
        var payload = new
        {
            model = model != string.Empty ? model : _model,
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

    public async Task<string> GetAnswerWithImagesAsync(object request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

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
                .GetString() ?? "Brak odpowiedzi";
        }

        return GetType().Name + " GetAnswerWithImagesAsync Error";
    }

    public async Task<string> AnalyzeImagesWithPromptAsync(IEnumerable<string> imagePaths, string userPrompt, string model = "gpt-4o", double temperature = 0.2)
    {
        var contentList = new List<object>
        {
            new
            {
                type = "text",
                text = userPrompt
            }
        };


        foreach (var path in imagePaths)
        {
            string base64;

            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                //get external file from URL
                using var httpClient = new HttpClient();
                var imageBytes = await httpClient.GetByteArrayAsync(path);
                base64 = Convert.ToBase64String(imageBytes);
            }
            else
            {
                //local file
                var imageBytes = await File.ReadAllBytesAsync(path);
                base64 = Convert.ToBase64String(imageBytes);
            }

            contentList.Add(new
            {
                type = "image_url",
                image_url = new
                {
                    url = $"data:image/png;base64,{base64}"
                }
            });
        }

        var payload = new
        {
            model,
            messages = new[]
            {
            new
            {
                role = "user",
                content = contentList
            }
        },
            temperature
        };

        return await GetAnswerWithImagesAsync(payload);
    }



    public async Task<string> TranscribeWithOpenAI(string filePathOrUrl)
    {
        string tempFilePath = null;
        Stream fileStream;

        try
        {
            if (filePathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                //get file from External URL
                using var httpClient = new HttpClient();
                var bytes = await httpClient.GetByteArrayAsync(filePathOrUrl);
                tempFilePath = Path.GetTempFileName();
                await File.WriteAllBytesAsync(tempFilePath, bytes);
                fileStream = File.OpenRead(tempFilePath);
            }
            else
            {
                //local file
                fileStream = File.OpenRead(filePathOrUrl);
            }

            var fileName = Path.GetFileName(filePathOrUrl);

            using var form = new MultipartFormDataContent();
            form.Add(new StreamContent(fileStream), "file", fileName);
            form.Add(new StringContent("whisper-1"), "model");

            if (_client != null)
            {
                var response = await _client.PostAsync("audio/transcriptions", form);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();

                var parsed = JsonDocument.Parse(responseBody);
                if (parsed.RootElement.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? " Missing text response.";
                }
            }

            return "GetAnswerAsync Error";
        }
        finally
        {
            // Wyczyść tymczasowy plik
            if (tempFilePath != null && File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    public async Task<string> CreateImageAsync(string prompt, string model = "dall-e-3", string size = "1024x1024", int numberOfImagesToGenerate = 1)
    {
        var isGptImageModel = model == "gpt-image-1";

        var payload = new Dictionary<string, object>
        {
            ["model"] = model,
            ["prompt"] = prompt,
            ["n"] = numberOfImagesToGenerate,
            ["size"] = size
        };

        if (!isGptImageModel)
        {
            payload["response_format"] = "url";
        }

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        if (_client != null)
        {
            var response = await _client.PostAsync("images/generations", content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var parsed = JsonDocument.Parse(responseBody);
            var firstData = parsed.RootElement.GetProperty("data")[0];

            if (firstData.TryGetProperty("b64_json", out var base64))
            {
                return base64.GetString() ?? "Brak obrazu (base64)";
            }
            else if (firstData.TryGetProperty("url", out var url))
            {
                return url.GetString() ?? "Brak obrazu (url)";
            }
            else
            {
                return "Nieznany format odpowiedzi";
            }
        }

        return GetType().Name + "CreateImageAsync Error";
    }

    public async Task<OpenAIEmbeddingResponseModel> GetEmbeddingAsync(string text, string model = "text-embedding-3-large")
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));
        }

        var payload = new
        {
            model = model,
            input = text
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _client.PostAsync("embeddings", content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var embeddingResponse = JsonSerializer.Deserialize<OpenAIEmbeddingResponseModel>(responseBody, options);
            if (embeddingResponse?.Data == null || embeddingResponse.Data.Length == 0)
            {
                Console.WriteLine($"Error: No embedding data in OpenAI response: {responseBody}");
                throw new Exception("No embedding data in OpenAI response.");
            }

            return embeddingResponse;

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Embedding Error: {ex.Message}");
            throw;
        }
    }
}