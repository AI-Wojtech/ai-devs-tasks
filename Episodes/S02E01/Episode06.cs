
using System.IO.Compression;
using System.Text;
using System.Text.Json;

public class Episode06 : EpisodeBase
{
    public override string Name => "S02E01 — Audio (Episode06)";
    public override string Description => "Znajdź ulicę instytutu profesora Maja na podstawie nagrań. (transkrypcja,whisper)";
    private static readonly string AudioFolder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Episodes", "S02E01", "audio");


    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";
    private const string LocalWhisperEndpoint = "http://localhost:1234/v1/audio/transcriptions";
    private const string LocalLLMEndpoint = "http://localhost:1234/v1/chat/completions";

    public override async Task RunAsync()
    {
        string apiKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");

        var transcriptions = new List<string>();
        var audioFiles = Directory.GetFiles(AudioFolder, "*.m4a");
        var openAI = new OpenAIService();
        foreach (var file in audioFiles)
        {
            Console.WriteLine($"Transkrybuję: {Path.GetFileName(file)}");
            var transcript = await openAI.TranscribeWithOpenAI(file);
            transcriptions.Add($"[{Path.GetFileNameWithoutExtension(file)}]\n{transcript}");
        }

        string fullTranscript = string.Join("\n---\n", transcriptions);
        string prompt = BuildPrompt(fullTranscript);


        string streetName = await openAI.GetAnswerAsync(prompt);

        var payload = new
        {
            task = "mp3",
            apikey = apiKey,
            answer = streetName
        };

        var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(
            ReportUrl,
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        );

        string responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine("ODPOWIEDŹ Z CENTRALI:\n" + responseContent);
    }

    private string BuildPrompt(string transcripts)
    {
        return $@"
        Analizuj poniższy tekst przesłuchań świadków dotyczących profesora Arkadiusza Maja (lub Andrzeja Maja). Wyodrębnij nazwę ulicy, na której znajduje się instytut, gdzie wykładał profesor, opierając się na najbardziej spójnych informacjach. Odpowiedz wyłącznie nazwą ulicy w formacie: ul. Nazwa ulicy. Ignoruj informacje niezwiązane z lokalizacją uczelni i sprzeczne wzmianki o innych miejscach.
        
        Przykład poprawnej odpowiedzi:
        ul. Mickiewicza

        <transkrypcje>
        {transcripts}
        </transkrypcje>
        ";
    }
}