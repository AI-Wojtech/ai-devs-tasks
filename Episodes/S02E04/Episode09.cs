using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

public class Episode09 : EpisodeBase
{
    public override string Name => "S02E04 — Połączenie wielu formatów (Episode09)";
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

        // Jeśli folder pusty, pobierz i rozpakuj
        if (!Directory.EnumerateFileSystemEntries(extractPath).Any())
        {
            using var client = new HttpClient();
            var data = await client.GetByteArrayAsync(DownloadUrl);
            await File.WriteAllBytesAsync(zipPath, data);
            ZipFile.ExtractToDirectory(zipPath, extractPath);
        }

        var validExtensions = new[] { ".txt", ".png", ".mp3" };

        var allFiles = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains(Path.Combine(extractPath, "facts")) &&
                Path.GetFileName(path) != "weapons_tests.zip" &&
                Path.HasExtension(path) &&
                validExtensions.Contains(Path.GetExtension(path).ToLower()))
            .ToList();

        var extractedTextByFile = new Dictionary<string, string>();
        var openAI = new OpenAIService();

        foreach (var file in allFiles)
        {
            var ext = Path.GetExtension(file).ToLower();
            var fileName = Path.GetFileName(file);

            if (ext == ".txt")
            {
                var text = await File.ReadAllTextAsync(file);
                extractedTextByFile[fileName] = text;
            }
            else if (ext == ".png")
            {
                var text = await openAI.AnalyzeImagesWithPromptAsync(new[] { file }, "Wyodrębnij informacje tekstowe z obrazu.");
                extractedTextByFile[fileName] = text;
            }
            else if (ext == ".mp3")
            {
                var text = await openAI.TranscribeWithOpenAI(file);
                extractedTextByFile[fileName] = text;
            }
        }

        var people = new List<string>();
        var hardware = new List<string>();

        foreach (var (fileName, content) in extractedTextByFile)
        {
            var category = await AskCategoryAsync(content);

            if (category == "people")
                people.Add(fileName);
            else if (category == "hardware")
                hardware.Add(fileName);
        }

        var payload = new Payload
        {
            Task = "kategorie",
            ApiKey = apiKey,
            Answer = new Answer
            {
                People = people.OrderBy(f => f).ToList(),
                Hardware = hardware.OrderBy(f => f).ToList()
            }
        };

        var json = JsonConvert.SerializeObject(payload, Formatting.Indented);

        var postContent = new StringContent(json, Encoding.UTF8, "application/json");
        using var http = new HttpClient();
        var response = await http.PostAsync(ReportUrl, postContent);
        var responseText = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"Wysłano dane, status: {response.StatusCode}");
        Console.WriteLine(responseText);
    }

    private async Task<string> AskCategoryAsync(string content)
    {
        var openAI = new OpenAIService();

        var prompt = $"""
        Twoim zadaniem jest skategoryzowanie opisu jako należącego do kategorii "people" lub "hardware" zgodnie z poniższymi regułami:
        - **People**: Włącz tylko notatki, które jasno wskazują na schwytanie ludzi (np. aresztowanie, identyfikacja osoby poprzez imię, odciski palców lub skan biometryczny) lub bezpośrednie ślady ich obecności (np. nadajniki zidentyfikowane jako należące do osoby). Wzmianki o zespole, prośby o dostawy lub ogólne odniesienia do ludzi bez konkretnych śladów ich aktywności nie kwalifikują się.
        - **Hardware**: Włącz tylko notatki dotyczące fizycznych usterek sprzętu (np. uszkodzenia anteny, czujników, przewodów, ogniw) lub ich naprawy/wymiany. Wyklucz problemy softwarowe, takie jak aktualizacje oprogramowania, algorytmów lub systemów komunikacji.
        Jeśli opis nie pasuje do żadnej z tych kategorii, zwróć słowo "none". Nie twórz żadnych dodatkowych kategorii. Zwróć tylko jedno słowo: "people", "hardware" lub "none".
        ---
        {content}
        ---
        """;

        var answer = await openAI.GetAnswerAsync(prompt);
        var result = answer?.Trim().ToLower();

        return result switch
        {
            "people" => "people",
            "hardware" => "hardware",
            _ => "none"
        };
    }


}