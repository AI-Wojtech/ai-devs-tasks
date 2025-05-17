using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public class Episode03 : EpisodeBase
{
    public override string Name => "S01E03 — Limity Dużych Modeli językowych i API";
    public override string Description => "Musisz poprawicplik kalibracyjny dla jednego z robotów przemysłowych. To dość popularny w 2024 roku format JSON. Dane testowe zawierają prawdopodobnie błędne obliczenia oraz luki w pytaniach otwartych. Popraw proszę ten plik i prześlij nam go juz po poprawkach. Tylko uważaj na rozmiar kontekstu modeli LLM, z którymi pracujesz — plik się nie zmieści w tym limicie.";

    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";

    public override async Task RunAsync()
    {
        string centralaApiKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
        string jsonDownloadUrl = $"https://c3ntrala.ag3nts.org/data/{centralaApiKey}/json.txt";
        using var client = new HttpClient();
        var jsonText = await client.GetStringAsync(jsonDownloadUrl);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        var root = JsonSerializer.Deserialize<CalibrationRoot>(jsonText, options);

        var openAI = new OpenAIService();
        foreach (var item in root.TestData)
        {
            // Napraw obliczenia matematyczne
            if (!string.IsNullOrEmpty(item.Question) && Regex.IsMatch(item.Question, @"^\d+\s*[\+\-\*/]\s*\d+$"))
            {
                try
                {
                    item.Answer = EvaluateExpression(item.Question);
                }
                catch { /* zostaw jak jest */ }
            }

            // Uzupełnij testy tekstowe
            if (item.Test?.Q != null && (string.IsNullOrWhiteSpace(item.Test.A) || item.Test.A == "???"))
            {
                var systemPrompt = @"
                You are an agent answering a question.
                Your task is to provide ONLY the correct value as plain English text — no explanations, no formatting, no JSON, no punctuation.

                <Rules>
                - Respond ONLY with the value — it will be inserted into a JSON field.
                - All answers must be in ENGLISH.
                - If the question is mathematical or logical — provide the correct answer.
                - If the question contains multiple parts, answer the part that is factual and answerable.
                </Rules>

                QUESTION:
                ";

                var answer = await openAI.GetAnswerAsync(item.Test.Q + systemPrompt);
                item.Test.A = answer?.Trim();
            }
        }

        var payload = new FinalPayload
        {
            task = "JSON",
            apikey = centralaApiKey,
            answer = new AnswerModel
            {
                apikey = centralaApiKey,
                description = root.Description,
                copyright = root.Copyright,
                TestData = root.TestData
            }
        };

        var httpSerivce = new HttpService();
        var response = await httpSerivce.PostJsonAsync<FinalPayload, object>(ReportUrl, payload);

    }

    private int EvaluateExpression(string expr)
    {
        var match = Regex.Match(expr, @"(\d+)\s*([\+\-\*/])\s*(\d+)");
        if (!match.Success) throw new FormatException("Invalid expression");
        int a = int.Parse(match.Groups[1].Value);
        int b = int.Parse(match.Groups[3].Value);
        return match.Groups[2].Value switch
        {
            "+" => a + b,
            "-" => a - b,
            "*" => a * b,
            "/" => a / b,
            _ => throw new InvalidOperationException("Unknown operator")
        };
    }
}


