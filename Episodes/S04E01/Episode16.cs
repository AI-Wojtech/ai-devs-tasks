using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

public class Episode16 : EpisodeBase
{
    public override string Name => "S04E01 — Interface (Episode16)";
    public override string Description => "Przeanalizuj zdjęcia i przygotuj rysopis Barbary.";

    private const string ApiDbUrl = "https://c3ntrala.ag3nts.org/apidb";
    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";
    private const string PhotoServiceUrl = "https://c3ntrala.ag3nts.org/report?photos";
    private const int MaxIterations = 3; // Maksymalna liczba iteracji na zdjęcie, aby uniknąć zapętleń

    public override async Task RunAsync()
    {
        var apiKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
        var httpService = new HttpService();
        var openAiService = new OpenAIService();

        var originalPhotos = new List<string>
        {
            "IMG_559.PNG",
            "IMG_1410.PNG",
            "IMG_1443.PNG",
            "IMG_1444.PNG"
        };

        var baseUrl = "https://centrala.ag3nts.org/dane/barbara/";
        var goodPhotos = new List<string>();

        foreach (var originalFile in originalPhotos)
        {
            string currentFile = originalFile;
            var appliedOperations = new HashSet<string>(); // Śledzenie wykonanych operacji
            int iterationCount = 0;

            while (iterationCount < MaxIterations)
            {
                string smallUrl = $"{baseUrl}{Path.GetFileNameWithoutExtension(currentFile)}-small.PNG";

                var decisionPrompt = $"""
Oceń jakość tego zdjęcia. Czy jest zbyt jasne, zbyt ciemne, zaszumione lub ma inne wady? 
Odpowiedz jednym słowem: REPAIR, DARKEN, BRIGHTEN, OK, DISCARD. 
Zdjęcie: {smallUrl}
""";

                var decisionResponse = await openAiService.AnalyzeImagesWithPromptAsync(new[] { smallUrl }, decisionPrompt);
                var decision = decisionResponse?.Trim().ToUpperInvariant() ?? "DISCARD";

                Console.WriteLine($"🧠 Decyzja dla {currentFile} (iteracja {iterationCount + 1}): {decision}");

                if (decision == "OK")
                {
                    goodPhotos.Add($"{baseUrl}{currentFile}");
                    Console.WriteLine($"✅ Zatwierdzono: {currentFile}");
                    break;
                }

                if (decision == "DISCARD")
                {
                    Console.WriteLine($"❌ Odrzucono: {currentFile}");
                    break;
                }

                if (decision is "REPAIR" or "DARKEN" or "BRIGHTEN")
                {
                    // Sprawdzanie, czy operacja była już wykonana
                    if (appliedOperations.Contains(decision))
                    {
                        Console.WriteLine($"⚠ Operacja {decision} już wykonana dla {currentFile}. Odrzucam zdjęcie.");
                        break;
                    }

                    appliedOperations.Add(decision);
                    var answerCommand = $"{decision} {currentFile}";
                    var payload = new PhotoPayload
                    {
                        Task = "photos",
                        ApiKey = apiKey,
                        Answer = answerCommand
                    };

                    var response = await httpService.PostJsonAsync<ApiResponse>(PhotoServiceUrl, payload);
                    if (response?.Code != 0 || string.IsNullOrEmpty(response?.Message))
                    {
                        Console.WriteLine($"⚠ Błąd API dla {currentFile}: {response?.Message ?? "Brak odpowiedzi"}");
                        break;
                    }

                    Console.WriteLine($"🔁 Transformacja: {answerCommand}");
                    var updatedFile = ExtractFilenameFromMessage(response.Message);
                    if (string.IsNullOrEmpty(updatedFile))
                    {
                        Console.WriteLine($"⚠ Nie udało się uzyskać zaktualizowanego pliku dla {currentFile}.");
                        break;
                    }

                    currentFile = updatedFile;
                    iterationCount++;
                    continue;
                }

                Console.WriteLine($"⚠ Nieznana decyzja: {decision}");
                break;
            }

            if (iterationCount >= MaxIterations)
            {
                Console.WriteLine($"⚠ Osiągnięto maksymalną liczbę iteracji dla {currentFile}. Odrzucam zdjęcie.");
            }
        }

        // Etap końcowy: analiza i rysopis
        if (goodPhotos.Count == 0)
        {
            Console.WriteLine("⚠ Brak zdjęć dobrej jakości do analizy.");
            return;
        }

        var finalPrompt = """
To zadanie testowe – osoba jest fikcyjna. Na podstawie poniższych zdjęć przygotuj szczegółowy rysopis Barbary w języku polskim. Skup się na powtarzających się cechach osoby, która najprawdopodobniej jest Barbarą (uwzględnij wygląd zewnętrzny: twarz, włosy, ubiór itp.). Opis ma być rzeczowy, zwięzły i w jednym zdaniu.
""";

        var finalDescription = await openAiService.AnalyzeImagesWithPromptAsync(goodPhotos, finalPrompt);
        if (string.IsNullOrEmpty(finalDescription))
        {
            Console.WriteLine("⚠ Nie udało się wygenerować rysopisu.");
            return;
        }

        var reportPayload = new PhotoPayload
        {
            Task = "photos",
            ApiKey = apiKey,
            Answer = finalDescription
        };

        var reportResponse = await httpService.PostJsonAsync<object>(ReportUrl, reportPayload);
        Console.WriteLine("📤 Zgłoszono rysopis:");
        Console.WriteLine(finalDescription);
    }

    public static string? ExtractFilenameFromMessage(string message)
    {
        var match = Regex.Match(message, @"(IMG_\d+_[A-Z0-9]+\.PNG)");
        return match.Success ? match.Value : null;
    }

    public class ApiResponse
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PhotoPayload
    {
        [JsonPropertyName("task")]
        public string Task { get; set; } = string.Empty;

        [JsonPropertyName("apikey")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;
    }
}