using System.Text;
using Newtonsoft.Json;

public class Episode10 : EpisodeBase
{
    public override string Name => "S02E05 — Multimodalność w praktyce (Episode10)";
    public override string Description => "Analiza artkułu zawierającego tekst, zdjęcia i nagrania audio oraz odpowiedź na pytania dotyczące wszystkich formatów tresci.";
    private const string ArticleUrl = "https://c3ntrala.ag3nts.org/dane/arxiv-draft.html";
    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";
    private OpenAIService _openAiService;
    private HttpClient _httpClient;

    public Episode10()
    {
        _openAiService = new OpenAIService();
        _httpClient = new HttpClient();
    }

    public override async Task RunAsync()
    {
        string apiKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
        var questionsUrl = $"https://c3ntrala.ag3nts.org/data/{apiKey}/arxiv.txt";

        var html = await HtmlHelper.DownloadHtmlAsync(ArticleUrl);
        var markdownText = HtmlHelper.ConvertHtmlToMarkdown(html);

        var imageDescriptions = await ProcessImagesAsync(html);
        var transcriptions = await ProcessAudioAsync(html);

        var context = BuildFullContext(markdownText, imageDescriptions, transcriptions);
        var questions = await DownloadQuestionsAsync(questionsUrl);
        var answers = await GenerateAnswersAsync(context, questions);

        var responseText = await SendAnswersToCentralaAsync(apiKey, answers);
    }


    private async Task<List<string>> ProcessImagesAsync(string html)
    {
        var baseUrl = "https://c3ntrala.ag3nts.org/dane/";
        var imagesDetails = HtmlHelper.ExtractImageUrls(html, baseUrl);
        var descriptions = new List<string>();

        foreach (var image in imagesDetails)
        {

            var prompt = $"Opisz co znajduje się na obrazie. Uwzględnij kontekst: \"{image.Caption}\". Opisz krótko i rzeczowo.";
            var description = await _openAiService.AnalyzeImagesWithPromptAsync(new[] { image.Url }, prompt);
            descriptions.Add($"**Obraz:** {description}");
        }

        return descriptions;
    }

    private async Task<List<string>> ProcessAudioAsync(string html)
    {
        var baseUrl = "https://c3ntrala.ag3nts.org/dane/";
        var audioUrls = HtmlHelper.ExtractAudioUrls(html);
        var transcriptions = new List<string>();

        foreach (var audioUrl in audioUrls)
        {
            var fullUrl = audioUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? audioUrl
                : new Uri(new Uri(baseUrl), audioUrl).ToString();

            var transcription = await _openAiService.TranscribeWithOpenAI(fullUrl);
            transcriptions.Add($"**Transkrypcja:** {transcription}");
        }

        return transcriptions;
    }


    private string BuildFullContext(string markdownText, List<string> imageDescriptions, List<string> transcriptions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Tekst artykułu:");
        sb.AppendLine(markdownText);
        sb.AppendLine("\n### Opisy obrazów:");
        foreach (var desc in imageDescriptions)
            sb.AppendLine($"- {desc}");

        sb.AppendLine("\n### Transkrypcje audio:");
        foreach (var tr in transcriptions)
            sb.AppendLine($"- {tr}");

        return sb.ToString();
    }

    private async Task<Dictionary<string, string>> DownloadQuestionsAsync(string questionsUrl)
    {
        var text = await HtmlHelper.DownloadTextAsync(questionsUrl);
        return QuestionParser.Parse(text);
    }

    private async Task<Dictionary<string, string>> GenerateAnswersAsync(string context, Dictionary<string, string> questions)
    {
        var systemPrompt = @"
        Jesteś asystentem, który odpowiada na pytania na podstawie podanego kontekstu.
        Odpowiedzi mają być dokładne, zwięzłe (jedno zdanie na pytanie) i w języku polskim.
        Bazuj wyłącznie na informacjach z podanego kontekstu, koncentrując się na sekcji związanej z pytaniem i ignorując nieistotne fragmenty, takie jak opisy obrazów, chyba że pytanie wyraźnie ich dotyczy.
        Unikaj mieszania informacji z różnych części kontekstu, np. lokalizacji związanych z innymi wydarzeniami.
        Zwróć odpowiedzi w formacie JSON, gdzie klucz to identyfikator pytania (np. '01'), a wartość to odpowiedź.
        Jeśli odpowiedź na pytanie jest nieznana, wpisz 'Nieznana odpowiedź' dla tego pytania.
        Zwróć wyłącznie obiekt JSON, bez dodatkowych treści.
        ";

        var userPrompt = $@"Kontekst:
        {context}

        Pytania:
        {string.Join("\n", questions.Select(q => $"{q.Key}={q.Value}"))}

        Proszę podać odpowiedzi w formacie JSON, opierając się wyłącznie na odpowiednich fragmentach kontekstu i unikając mieszania informacji z innych części, takich jak inne lokalizacje lub wydarzenia:
        ";

        string rawAnswer = await _openAiService.GetAnswerAsync(userPrompt, systemPrompt);

        try
        {
            var answers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(rawAnswer);
            return answers ?? new Dictionary<string, string>();
        }
        catch (System.Text.Json.JsonException ex)
        {
            Console.WriteLine("Błąd parsowania JSON: " + ex.Message);
            Console.WriteLine("Odpowiedź modelu: " + rawAnswer);
            return new Dictionary<string, string>();
        }
    }


    private async Task<string> SendAnswersToCentralaAsync(string apiKey, Dictionary<string, string> answers)
    {
        var payload = new
        {
            task = "arxiv",
            apikey = apiKey,
            answer = answers
        };

        var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
        var postContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(ReportUrl, postContent);
        return await response.Content.ReadAsStringAsync();
    }

    public static class QuestionParser
    {
        public static Dictionary<string, string> Parse(string text)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(text))
                return dict;

            // Rozdziel na linie
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        dict[key] = value;
                }
            }

            return dict;
        }
    }


}