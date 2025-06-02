using System.IO.Compression;
using System.Text.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Newtonsoft.Json;

public class Episode11 : EpisodeBase
{
    public override string Name => "S03E01 — Dokumenty (Episode11)";
    public override string Description => "Twoim zadaniem jest analiza formatów TXT, PNG i MP3 oraz ich kategoryzacja.";
    private const string DownloadUrl = "https://c3ntrala.ag3nts.org/dane/pliki_z_fabryki.zip";
    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";

    public override async Task RunAsync()
    {
        string apiKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
        var tempRoot = Path.GetTempPath();
        var zipPath = Path.Combine(tempRoot, "fabryka.zip");
        var extractPath = Path.Combine(tempRoot, "fabryka");

        if (!Directory.Exists(extractPath))
        {
            Directory.CreateDirectory(extractPath);
        }

        if (!Directory.EnumerateFileSystemEntries(extractPath).Any())
        {
            using var client = new HttpClient();
            var data = await client.GetByteArrayAsync(DownloadUrl);
            await File.WriteAllBytesAsync(zipPath, data);
            ZipFile.ExtractToDirectory(zipPath, extractPath);
        }

        var reportsPath = extractPath;
        var factsPath = Path.Combine(extractPath, "facts");

        var openAi = new OpenAIService();
        var analyzer = new ReportAnalyzer(reportsPath, factsPath, openAi);
        var answers = await analyzer.AnalyzeAsync();
        var answersString = answers.ToDictionary(
            pair => pair.Key,
            pair => string.Join(",", pair.Value)
        );
        var payload = new
        {
            task = "dokumenty",
            apikey = apiKey,
            answer = answersString
        };

        var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
        var postContent = new StringContent(json, Encoding.UTF8, "application/json");
        using var http = new HttpClient();
        var response = await http.PostAsync(ReportUrl, postContent);
        var responseText = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"Wysłano dane, status: {response.StatusCode}");
        Console.WriteLine(responseText);
    }
}
