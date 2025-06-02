// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Net.Http;
// using System.Text;
// using System.Text.Json;
// using System.Text.RegularExpressions;
// using System.Threading.Tasks;

// public class Episode12 : EpisodeBase
// {
//     private readonly HttpClient _httpClient = new HttpClient();
//     public override string Name => "S03E02 — Wyszukiwanie Semantyczne (Episode12)";
//     public override string Description => "Zaindeksuj pliki tekstowe z użyciem embeddingów w Qdrant i odpowiedz na pytanie";

//     private const string Question = "W raporcie, z którego dnia znajduje się wzmianka o kradzieży prototypu broni?";
//     private const string TextFilesFolder = @"D:\ai-dev\ai-devs-zadania-code\ai-devs-tasks\Episodes\S03E02\textData";
//     private const string CollectionName = "zad12_collection"; // Changed to match Python code
//     private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";

//     public override async Task RunAsync()
//     {
//         var qdrantUrl = ConfigHelper.GetValue<string>("Qdrant:URL").Replace(":6333", ":443");
//         var qdrantApiKey = ConfigHelper.GetValue<string>("Qdrant:API_KEY");
//         var openAi = new OpenAIService();

//         Console.WriteLine($"Connecting to Qdrant at: {qdrantUrl}");
//         _httpClient.DefaultRequestHeaders.Clear();
//         _httpClient.DefaultRequestHeaders.Add("api-key", qdrantApiKey);

//         try
//         {
//             await EnsureCollectionAsync(qdrantUrl);
//             await IndexReportsAsync(openAi, qdrantUrl);
//             var answer = await SearchAnswerAsync(openAi, qdrantUrl);
//             await SendAnswerAsync(answer);
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Error: {ex.Message}");
//             throw;
//         }
//         finally
//         {
//             _httpClient.Dispose();
//         }
//     }

//     private async Task EnsureCollectionAsync(string qdrantUrl)
//     {
//         var collectionUrl = $"{qdrantUrl}/collections/{CollectionName}";
//         try
//         {
//             // Check if collection exists
//             var response = await _httpClient.GetAsync(collectionUrl);
//             if (response.IsSuccessStatusCode)
//             {
//                 Console.WriteLine($"Collection {CollectionName} exists. Recreating to ensure clean state.");
//                 await _httpClient.DeleteAsync(collectionUrl);
//             }

//             // Create collection
//             var payload = new
//             {
//                 vectors = new { size = 3072, distance = "Cosine" }
//             };
//             var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
//             response = await _httpClient.PutAsync(collectionUrl, content);

//             if (!response.IsSuccessStatusCode)
//             {
//                 var error = await response.Content.ReadAsStringAsync();
//                 throw new Exception($"Failed to create collection: {response.StatusCode}, {error}");
//             }

//             Console.WriteLine($"Collection {CollectionName} created.");
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Error in EnsureCollectionAsync: {ex.Message}");
//             throw;
//         }
//     }

//     private async Task IndexReportsAsync(OpenAIService openAi, string qdrantUrl)
//     {
//         if (!Directory.Exists(TextFilesFolder))
//         {
//             throw new DirectoryNotFoundException($"Folder {TextFilesFolder} does not exist.");
//         }

//         var files = Directory.GetFiles(TextFilesFolder, "*.txt");
//         Console.WriteLine($"Found {files.Length} files to index.");

//         if (files.Length == 0)
//         {
//             Console.WriteLine("No text files found.");
//             return;
//         }

//         var points = new List<object>();
//         int index = 0;
//         foreach (var filePath in files)
//         {
//             try
//             {
//                 string fileName = Path.GetFileNameWithoutExtension(filePath);
//                 var dateMatch = Regex.Match(fileName, @"(\d{4}[-_]\d{2}[-_]\d{2})");
//                 if (!dateMatch.Success)
//                 {
//                     Console.WriteLine($"Skipping {fileName}: Invalid date format.");
//                     continue;
//                 }

//                 string date = dateMatch.Groups[1].Value.Replace('_', '-');
//                 string content = await File.ReadAllTextAsync(filePath);
//                 if (string.IsNullOrWhiteSpace(content))
//                 {
//                     Console.WriteLine($"Skipping {fileName}: Empty file.");
//                     continue;
//                 }

//                 var embeddingResult = await openAi.GetEmbeddingAsync(content);
//                 if (embeddingResult?.Data == null || !embeddingResult.Data.Any() || embeddingResult.Data[0].Embedding.Length != 3072)
//                 {
//                     Console.WriteLine($"Skipping {fileName}: Invalid embedding.");
//                     continue;
//                 }

//                 points.Add(new
//                 {
//                     id = index++,
//                     vector = embeddingResult.Data[0].Embedding,
//                     payload = new
//                     {
//                         data = date, // Match Python's 'data' field
//                         filename = fileName,
//                         full_filename = Path.GetFileName(filePath),
//                         text = content.Length > 1000 ? content.Substring(0, 1000) + "..." : content,
//                         path = filePath
//                     }
//                 });

//                 Console.WriteLine($"Prepared point for {fileName} (date: {date})");
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"Error processing {filePath}: {ex.Message}");
//             }
//         }

//         if (points.Count == 0)
//         {
//             Console.WriteLine("No valid points to index.");
//             return;
//         }

//         // Upsert points
//         try
//         {
//             var payload = new { points };
//             var contentJson = JsonSerializer.Serialize(payload);
//             var httpContent = new StringContent(contentJson, Encoding.UTF8, "application/json");
//             var upsertUrl = $"{qdrantUrl}/collections/{CollectionName}/points?wait=true";

//             Console.WriteLine($"Upserting {points.Count} points to {upsertUrl}");
//             var response = await _httpClient.PutAsync(upsertUrl, httpContent);

//             if (!response.IsSuccessStatusCode)
//             {
//                 var error = await response.Content.ReadAsStringAsync();
//                 Console.WriteLine($"Failed to upsert points: {response.StatusCode}, {error}");
//                 return;
//             }

//             Console.WriteLine($"Upserted {points.Count} points.");
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Error upserting points: {ex.Message}");
//         }

//         // Verify point count
//         try
//         {
//             var collectionUrl = $"{qdrantUrl}/collections/{CollectionName}";
//             var response = await _httpClient.GetAsync(collectionUrl);
//             if (response.IsSuccessStatusCode)
//             {
//                 var json = await response.Content.ReadAsStringAsync();
//                 var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
//                 var pointCount = ((JsonElement)data["result"]).GetProperty("points_count").GetInt64();
//                 Console.WriteLine($"Collection {CollectionName} contains {pointCount} points.");
//             }
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Error verifying points: {ex.Message}");
//         }
//     }

//     private async Task<string> SearchAnswerAsync(OpenAIService openAi, string qdrantUrl)
//     {
//         try
//         {
//             var embeddingResult = await openAi.GetEmbeddingAsync(Question);
//             if (embeddingResult?.Data == null || !embeddingResult.Data.Any())
//             {
//                 Console.WriteLine("Failed to generate embedding for question.");
//                 return "Brak wyników";
//             }

//             var payload = new
//             {
//                 vector = embeddingResult.Data[0].Embedding,
//                 limit = 3,
//                 with_payload = true
//             };
//             var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
//             var searchUrl = $"{qdrantUrl}/collections/{CollectionName}/points/search";

//             var response = await _httpClient.PostAsync(searchUrl, content);
//             if (!response.IsSuccessStatusCode)
//             {
//                 var error = await response.Content.ReadAsStringAsync();
//                 Console.WriteLine($"Search failed: {response.StatusCode}, {error}");
//                 return "Brak wyników";
//             }

//             var searchJson = await response.Content.ReadAsStringAsync();
//             var searchData = JsonSerializer.Deserialize<Dictionary<string, object>>(searchJson);
//             var results = ((JsonElement)searchData["result"]).EnumerateArray()
//                 .Select(r => new
//                 {
//                     Score = r.GetProperty("score").GetDouble(),
//                     Payload = r.GetProperty("payload").Deserialize<Dictionary<string, string>>()
//                 }).ToList();

//             if (results.Count == 0 || !results[0].Payload.ContainsKey("data"))
//             {
//                 Console.WriteLine("No matching results found.");
//                 return "Brak wyników";
//             }

//             var date = results[0].Payload["data"];
//             Console.WriteLine($"Found date: {date}");
//             return date;
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Search error: {ex.Message}");
//             return "Brak wyników";
//         }
//     }

//     private async Task SendAnswerAsync(string answer)
//     {
//         try
//         {
//             var payload = new
//             {
//                 task = "wektory",
//                 apikey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY"),
//                 answer
//             };
//             var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
//             var response = await _httpClient.PostAsync(ReportUrl, content);
//             var responseJson = await response.Content.ReadAsStringAsync();
//             Console.WriteLine($"Central response: {responseJson}");

//             var data = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson);
//             if (data.ContainsKey("code") && data["code"].ToString() == "0")
//             {
//                 Console.WriteLine($"Received flag: {data["message"]}");
//             }
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Error sending answer: {ex.Message}");
//         }
//     }
// }

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class Episode12 : EpisodeBase
{
    private readonly HttpClient _httpClient = new HttpClient();
    public override string Name => "S03E02 — Wyszukiwanie Semantyczne (Episode12)";
    public override string Description => "Zaindeksuj pliki tekstowe z użyciem embeddingów w Qdrant i odpowiedz na pytanie";

    private const string Question = "W raporcie, z którego dnia znajduje się wzmianka o kradzieży prototypu broni?";
    private const string TextFilesFolder = @"D:\ai-dev\ai-devs-zadania-code\ai-devs-tasks\Episodes\S03E02\textData";
    private const string CollectionName = "zad12_collection";
    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";

    public override async Task RunAsync()
    {
        var qdrantUrl = ConfigHelper.GetValue<string>("Qdrant:URL").Replace(":6333", ":443");
        var qdrantApiKey = ConfigHelper.GetValue<string>("Qdrant:API_KEY");
        var openAi = new OpenAIService();

        _httpClient.DefaultRequestHeaders.Add("api-key", qdrantApiKey);

        try
        {
            await EnsureCollectionAsync(qdrantUrl);
            await IndexReportsAsync(openAi, qdrantUrl);
            var answer = await SearchAnswerAsync(openAi, qdrantUrl);
            await SendAnswerAsync(answer, "wektory");
        }
        finally
        {
            _httpClient.Dispose();
        }
    }

    private async Task EnsureCollectionAsync(string qdrantUrl)
    {
        var collectionUrl = $"{qdrantUrl}/collections/{CollectionName}";
        var response = await _httpClient.GetAsync(collectionUrl);
        if (response.IsSuccessStatusCode)
        {
            await _httpClient.DeleteAsync(collectionUrl);
        }

        var payload = new { vectors = new { size = 3072, distance = "Cosine" } };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _httpClient.PutAsync(collectionUrl, content);
    }

    private async Task IndexReportsAsync(OpenAIService openAi, string qdrantUrl)
    {
        var files = Directory.GetFiles(TextFilesFolder, "*.txt");
        if (files.Length == 0)
        {
            return;
        }

        var points = new List<object>();
        int index = 0;
        foreach (var filePath in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            var dateMatch = Regex.Match(fileName, @"(\d{4}[-_]\d{2}[-_]\d{2})");
            if (!dateMatch.Success)
            {
                continue;
            }

            string date = dateMatch.Groups[1].Value.Replace('_', '-');
            string content = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var embeddingResult = await openAi.GetEmbeddingAsync(content);
            if (embeddingResult?.Data == null || !embeddingResult.Data.Any() || embeddingResult.Data[0].Embedding.Length != 3072)
            {
                continue;
            }

            points.Add(new
            {
                id = index++,
                vector = embeddingResult.Data[0].Embedding,
                payload = new
                {
                    data = date,
                    filename = fileName,
                    full_filename = Path.GetFileName(filePath),
                    text = content.Length > 1000 ? content.Substring(0, 1000) + "..." : content,
                    path = filePath
                }
            });
        }

        if (points.Count == 0)
        {
            return;
        }

        var payload = new { points };
        var contentJson = JsonSerializer.Serialize(payload);
        var httpContent = new StringContent(contentJson, Encoding.UTF8, "application/json");
        var upsertUrl = $"{qdrantUrl}/collections/{CollectionName}/points?wait=true";
        await _httpClient.PutAsync(upsertUrl, httpContent);
    }

    private async Task<string> SearchAnswerAsync(OpenAIService openAi, string qdrantUrl)
    {
        var embeddingResult = await openAi.GetEmbeddingAsync(Question);
        if (embeddingResult?.Data == null || !embeddingResult.Data.Any())
        {
            return "Brak wyników";
        }

        var payload = new
        {
            vector = embeddingResult.Data[0].Embedding,
            limit = 3,
            with_payload = true
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var searchUrl = $"{qdrantUrl}/collections/{CollectionName}/points/search";

        var response = await _httpClient.PostAsync(searchUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            return "Brak wyników";
        }

        var searchJson = await response.Content.ReadAsStringAsync();
        var searchData = JsonSerializer.Deserialize<Dictionary<string, object>>(searchJson);
        var results = ((JsonElement)searchData["result"]).EnumerateArray()
            .Select(r => new
            {
                Payload = r.GetProperty("payload").Deserialize<Dictionary<string, string>>()
            }).ToList();

        return results.Count == 0 || !results[0].Payload.ContainsKey("data") ? "Brak wyników" : results[0].Payload["data"];
    }

    private async Task SendAnswerAsync(string answer, string taskName)
    {
        var payload = new
        {
            task = taskName,
            apikey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY"),
            answer
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var result = await _httpClient.PostAsync(ReportUrl, content);
    }
}