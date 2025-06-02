using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public class Episode12v1 : EpisodeBase
{
    public override string Name => "S03E02 — Wyszukiwanie Semantyczne (Episode12)";
    public override string Description => "Zaindeksuj pliki tekstowe z użyciem embeddingów w swojej bazie wektorowej Qdrant, a następnie odpowiedz na pytanie";

    public const string Question = "W raporcie, z którego dnia znajduje się wzmianka o kradzieży prototypu broni?";
    private const string TextFilesFolder = @"D:\ai-dev\ai-devs-zadania-code\ai-devs-tasks\Episodes\S03E02\textData";
    private const string CollectionName = "ai-dev-collection-ws";
    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";

    private readonly HttpClient _httpClient;

    public Episode12v1()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public override async Task RunAsync()
    {
        var qdrantUrlString = ConfigHelper.GetValue<string>("Qdrant:URL");
        var qdrantApiKey = ConfigHelper.GetValue<string>("Qdrant:API_KEY");
        var openAi = new OpenAIService();

        // Correct port to 443 for REST API
        qdrantUrlString = qdrantUrlString.Replace(":6333", ":443");
        Console.WriteLine($"Using Qdrant URL: {qdrantUrlString}");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("api-key", qdrantApiKey);

        try
        {
            await EnsureCollectionAsync(qdrantUrlString, CollectionName);
            await IndexReports(openAi, qdrantUrlString);
            string answer = await SearchAnswer(openAi, qdrantUrlString);
            await SendAnswer(answer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
        finally
        {
            _httpClient.Dispose();
        }
    }

    private async Task EnsureCollectionAsync(string qdrantUrl, string name)
    {
        try
        {
            Console.WriteLine("Testing Qdrant connection...");

            // Health check
            var healthResponse = await _httpClient.GetAsync($"{qdrantUrl}/health");
            if (!healthResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("Qdrant health check successful");
            }
            else
            {
                throw new HttpRequestException($"Health check failed: {healthResponse.StatusCode}");
            }

            // Check if collection exists
            Console.WriteLine("Listing collections...");
            var collectionsResponse = await _httpClient.GetAsync($"{qdrantUrl}/collections");
            collectionsResponse.EnsureSuccessStatusCode();
            var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
            var collectionsData = JsonSerializer.Deserialize<Dictionary<string, object>>(collectionsJson);
            var collections = ((JsonElement)collectionsData["result"]).GetProperty("collections")
                .EnumerateArray()
                .Select(c => c.GetProperty("name").GetString())
                .ToList();

            Console.WriteLine($"Found {collections.Count} collections");
            foreach (var col in collections)
            {
                Console.WriteLine($"  - {col}");
            }

            if (!collections.Contains(name))
            {
                Console.WriteLine($"Creating collection '{name}'...");
                var payload = new
                {
                    vectors = new
                    {
                        size = 3072, // for text-embedding-3-large
                        distance = "Cosine"
                    }
                };
                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var createResponse = await _httpClient.PutAsync($"{qdrantUrl}/collections/{name}", content);
                createResponse.EnsureSuccessStatusCode();
                Console.WriteLine($"Collection '{name}' created successfully.");
            }
            else
            {
                Console.WriteLine($"Collection '{name}' already exists.");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP Error: {ex.Message}, Status: {ex.StatusCode}");
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine("Connection issue. Please check:");
                Console.WriteLine("1. Is the Qdrant URL correct?");
                Console.WriteLine("2. Is the API key valid?");
                Console.WriteLine("3. Is the Qdrant Cloud instance accessible?");
            }
            throw new Exception($"Cannot connect to Qdrant: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General error with collection operations: {ex.Message}");
            throw;
        }
    }


    private async Task IndexReports(OpenAIService openAi, string qdrantUrl)
    {
        Console.WriteLine($"Indexing to Qdrant at {qdrantUrl}/collections/{CollectionName}");

        // Verify collection configuration
        try
        {
            var collectionUrl = $"{qdrantUrl}/collections/{CollectionName}";
            var response = await _httpClient.GetAsync(collectionUrl);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                var pointCount = ((JsonElement)data["result"]).GetProperty("points_count").GetInt64();
                var vectorSize = ((JsonElement)data["result"]).GetProperty("config").GetProperty("params").GetProperty("vectors").GetProperty("size").GetInt64();
                var distance = ((JsonElement)data["result"]).GetProperty("config").GetProperty("params").GetProperty("vectors").GetProperty("distance").GetString();
                Console.WriteLine($"Collection {CollectionName}: {pointCount} points, vector size: {vectorSize}, distance: {distance}");
                if (vectorSize != 3072)
                {
                    Console.WriteLine("Warning: Vector size mismatch. Expected 3072 for text-embedding-3-large.");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to verify collection: {response.StatusCode}, Response: {errorContent}");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error verifying collection: {ex.Message}");
            return;
        }

        // Test with a hardcoded point
        Console.WriteLine("Testing with a hardcoded point...");
        try
        {
            var testContent = "Kradzież prototypu broni zgłoszona 2024-01-01.";
            var embeddingResult = await openAi.GetEmbeddingAsync(testContent);
            if (embeddingResult?.Data == null || !embeddingResult.Data.Any() || embeddingResult.Data[0].Embedding.Length != 3072)
            {
                Console.WriteLine("Failed to generate test embedding or invalid length.");
                return;
            }

            var testPoint = new
            {
                id = Guid.NewGuid().ToString(),
                vector = embeddingResult.Data[0].Embedding,
                payload = new { date = "2024-01-01", fileName = "test", content = testContent }
            };

            var testPayload = new { points = new[] { testPoint } };
            var testJson = JsonSerializer.Serialize(testPayload, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"Test payload:\n{testJson}");

            var testContentJson = new StringContent(testJson, Encoding.UTF8, "application/json");
            var testUrl = $"{qdrantUrl}/collections/{CollectionName}/points?wait=true";
            var testResponse = await _httpClient.PutAsync(testUrl, testContentJson);

            Console.WriteLine($"Test upsert response: {testResponse.StatusCode}");
            if (!testResponse.IsSuccessStatusCode)
            {
                var errorContent = await testResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Test upsert failed: {errorContent}");
            }
            else
            {
                var responseJson = await testResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Test upsert succeeded: {responseJson}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test point error: {ex.Message}");
        }

        // Index files
        var files = Directory.GetFiles(TextFilesFolder, "*.txt");
        Console.WriteLine($"Found {files.Length} files.");
        if (files.Length == 0)
        {
            Console.WriteLine($"No files in {TextFilesFolder}.");
            return;
        }

        int indexedCount = 0;
        foreach (var filePath in files)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                var dateMatch = Regex.Match(fileName, @"(\d{4}[-_]\d{2}[-_]\d{2})");
                if (!dateMatch.Success)
                {
                    Console.WriteLine($"Skipping {fileName}: Invalid date format.");
                    continue;
                }

                string date = dateMatch.Groups[1].Value.Replace('_', '-');
                string content = await File.ReadAllTextAsync(filePath);
                if (string.IsNullOrWhiteSpace(content))
                {
                    Console.WriteLine($"Skipping {fileName}: Empty file.");
                    continue;
                }

                var embeddingResult = await openAi.GetEmbeddingAsync(content);
                if (embeddingResult?.Data == null || !embeddingResult.Data.Any() || embeddingResult.Data[0].Embedding.Length != 3072)
                {
                    Console.WriteLine($"Skipping {fileName}: Invalid embedding.");
                    continue;
                }

                var point = new
                {
                    id = Guid.NewGuid().ToString(),
                    vector = embeddingResult.Data[0].Embedding,
                    payload = new
                    {
                        date,
                        fileName,
                        content = content.Length > 1000 ? content.Substring(0, 1000) + "..." : content
                    }
                };

                var payload = new { points = new[] { point } };
                var contentJson = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(contentJson, Encoding.UTF8, "application/json");

                var upsertUrl = $"{qdrantUrl}/collections/{CollectionName}/points?wait=true";
                var upsertResponse = await _httpClient.PutAsync(upsertUrl, httpContent);

                if (!upsertResponse.IsSuccessStatusCode)
                {
                    var errorContent = await upsertResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to index {fileName}: {upsertResponse.StatusCode}, Response: {errorContent}");
                    continue;
                }

                Console.WriteLine($"Indexed {fileName} (date: {date})");
                indexedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error indexing {filePath}: {ex.Message}");
            }
        }

        Console.WriteLine($"Indexed {indexedCount} of {files.Length} files.");

        // Final point count
        try
        {
            var collectionUrl = $"{qdrantUrl}/collections/{CollectionName}";
            var response = await _httpClient.GetAsync(collectionUrl);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                var pointCount = ((JsonElement)data["result"]).GetProperty("points_count").GetInt64();
                Console.WriteLine($"Collection {CollectionName} contains {pointCount} points.");
                if (pointCount == 0)
                {
                    Console.WriteLine("Warning: No points indexed. Check errors above.");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to verify collection: {response.StatusCode}, Response: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error verifying collection: {ex.Message}");
        }
    }

    private async Task<string> SearchAnswer(OpenAIService openAi, string qdrantUrl)
    {
        try
        {
            Console.WriteLine($"Searching for: {Question}");

            // Generate embedding for question
            var embeddingResult = await openAi.GetEmbeddingAsync(Question);
            if (embeddingResult?.Data == null || !embeddingResult.Data.Any())
            {
                Console.WriteLine("Failed to generate embedding for question.");
                throw new Exception("Failed to generate embedding for question.");
            }

            Console.WriteLine($"Question embedding generated (length: {embeddingResult.Data[0].Embedding.Length}, first 5 values: [{string.Join(", ", embeddingResult.Data[0].Embedding.Take(5))}...])");

            // Search in Qdrant
            var payload = new
            {
                vector = embeddingResult.Data[0].Embedding,
                limit = 10,
                with_payload = true,
                score_threshold = 0.1 // Include lower-scoring matches
            };
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            Console.WriteLine($"Sending search request to: {qdrantUrl}/collections/{CollectionName}/points/search");
            var searchResponse = await _httpClient.PostAsync($"{qdrantUrl}/collections/{CollectionName}/points/search", content);
            if (!searchResponse.IsSuccessStatusCode)
            {
                var errorContent = await searchResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Search failed: {searchResponse.StatusCode}, Response: {errorContent}");
                throw new HttpRequestException($"Search failed: {searchResponse.StatusCode}, Response: {errorContent}");
            }

            var searchJson = await searchResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Raw search response: {searchJson}");

            var searchData = JsonSerializer.Deserialize<Dictionary<string, object>>(searchJson);
            var results = ((JsonElement)searchData["result"]).EnumerateArray().Select(r => new
            {
                Score = r.GetProperty("score").GetDouble(),
                Payload = r.GetProperty("payload").Deserialize<Dictionary<string, string>>()
            }).ToList();

            Console.WriteLine($"Found {results.Count} results");
            if (results.Count == 0)
            {
                Console.WriteLine("No results found. Possible issues:");
                Console.WriteLine("1. Collection may be empty or incorrect (check CollectionName: ai-dev-collection-ws).");
                Console.WriteLine("2. Embeddings may not match (verify OpenAIService consistency).");
                Console.WriteLine("3. Text files may not contain relevant content for the question.");
                return "Brak wyników";
            }

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result.Payload.ContainsKey("date") && result.Payload.ContainsKey("fileName"))
                {
                    Console.WriteLine($"Result {i + 1}: {result.Payload["fileName"]}, " +
                                      $"Date: {result.Payload["date"]}, " +
                                      $"Score: {result.Score}");
                }
                else
                {
                    Console.WriteLine($"Result {i + 1}: Missing 'date' or 'fileName' in payload. Payload: {JsonSerializer.Serialize(result.Payload)}");
                }
            }

            var bestResult = results.FirstOrDefault(r => r.Payload.ContainsKey("date"));
            if (bestResult == null)
            {
                Console.WriteLine("No result with 'date' field found. Returning fallback response.");
                return "Brak wyników";
            }

            string foundDate = bestResult.Payload["date"];
            Console.WriteLine($"Best match date: {foundDate}");
            return foundDate;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during search: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private async Task SendAnswer(string answer)
    {
        try
        {
            var payload = new
            {
                task = "wektory",
                apikey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY"),
                answer
            };

            Console.WriteLine($"Sending answer: {answer}");
            var httpService = new HttpService();
            var result = await httpService.PostJsonAsync<object>(ReportUrl, payload);
            Console.WriteLine($"Response received: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending answer: {ex.Message}");
            throw;
        }
    }
}