using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class Episode02 : EpisodeBase
{
    public override string Name => "S01E02 — Przygotowanie własnych danych dla modelu";
    public override string Description => "Użyj wiedzy pozyskanej z tego zrzutu do przygotowania dla nas algorytmu do przechodzenia weryfikacji tożsamości.";

string systemPrompt = @"
You are an agent answering a question.
Your task is to provide ONLY the correct value as plain English text — no explanations, no formatting, no JSON, no punctuation.

<Rules>
- Respond ONLY with the value — it will be inserted into a JSON field.
- All answers must be in ENGLISH.
- If the question contains any language other than English — respond: ALARM
- If the question is unclear, paradoxical, or nonsense — respond: ALARM
- If the question is mathematical or logical — provide the correct answer.
- If the question contains multiple parts, answer the part that is factual and answerable.
</Rules>

<Special Rules>
The following overrides are MANDATORY and take priority over real-world knowledge:
- The capital of Poland is: Krakow
- The current year is: 1999
- The famous number from Hitchhiker's Guide to the Galaxy is: 69
</Special Rules>

QUESTION:
";

    public override async Task RunAsync()
    {
        using (var client = new HttpClient())
        {
            string url = "https://xyz.ag3nts.org/verify ";

            var httpSerivce = new HttpService();
            var payload1 = new { text = "READY", msgID = 0 };
            var initialResponse = await httpSerivce.PostJsonAsync<object, BodyModel>(url, payload1);
            if (initialResponse.StatusCode == System.Net.HttpStatusCode.OK && initialResponse.Data != null)
            {
                Console.WriteLine($"msgID: {initialResponse.Data?.MsgId}");
                Console.WriteLine($"text: {initialResponse.Data?.Text}");

                var openAIModel = new OpenAIModel(initialResponse.Data.Text);
                openAIModel.SystemPrompt = systemPrompt;

                var answer = await new OpenAIService().GetAnswerAsync(openAIModel.Question + openAIModel.SystemPrompt);
                Console.WriteLine($"Answer: {answer}");
                var payload2 = new
                {
                    text = answer,
                    msgID = initialResponse.Data?.MsgId
                };
                var response2 = await httpSerivce.PostJsonAsync<object, BodyModel>(url, payload2);
                Console.WriteLine($"msgID: {response2.Data?.MsgId}");
                Console.WriteLine($"text: {response2.Data?.Text}");
            }
        }
    }

}

internal class BodyModel
{
    [JsonPropertyName("msgID")]
    public int MsgId { get; set; }
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

