using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class Episode17 : EpisodeBase
{
    public override string Name => "S04E02 — Fine Tuning (Episode17)";
    public override string Description => "Sprawdź dane przy użyciu własnego modelu fine-tuning i wyślij identyfikatory poprawnych.";

    private const string VerifyFilePath = @"D:\ai-dev\ai-devs-zadania-code\ai-devs-tasks\Episodes\S04E02\lab_data\verify.txt";
    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";
    private const string ModelName = "ft:gpt-4o-mini-2024-07-18:personal:episode17:BhORs00X";

    public override async Task RunAsync()
    {
        var centralaKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
        var lines = await File.ReadAllLinesAsync(VerifyFilePath);
        var correctIds = new List<string>();

        var fineTuningService = new FineTuningService();
        var openAiServie = new OpenAIService();
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var answer = await openAiServie.GetAnswerAsync(
                question: line,
                systemPrompt: "validate data",
                model: ModelName
            );

            var normalized = answer?.Trim().ToLowerInvariant();

            if (normalized == "1" || normalized == "true")
            {
                correctIds.Add((i + 1).ToString("D2")); // np. "01"
            }

            await Task.Delay(100); // żeby uniknąć throttlingu
        }

        var client = new HttpService();
        var response = await client.SendAnswerAsync(correctIds, "research");
    }
}
