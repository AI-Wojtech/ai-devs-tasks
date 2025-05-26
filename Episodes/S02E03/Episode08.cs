public class Episode08 : EpisodeBase
{
    public override string Name => "S02E03-Generowanie-i-modyfikacja-obrazow (Episode08)";
    public override string Description => "Twoim zadaniem jest stworzenie grafiki przedstawiającej robota zaobserwowanego w fabryce. Na podstawie opisu wygeneruj obraz robota i prześlij link do tego obrazu do Centrali..";

    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";

    public override async Task RunAsync()
    {
        string apiKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
        string robotDescriptionUrl = $"https://c3ntrala.ag3nts.org/data/{apiKey}/robotid.json%60";
        using var client = new HttpClient();
        var robotDescriptionJsonText = await client.GetStringAsync(robotDescriptionUrl);

        var openAi = new OpenAIService();
        var createdImage = await openAi.CreateImageAsync(robotDescriptionJsonText, "dall-e-3");

        var payload = new
        {
            task = "robotid",
            apikey = apiKey,
            answer = createdImage
        };


        var httpService = new HttpService();
        var result = await httpService.PostJsonAsync<object, object>(ReportUrl, payload);
    }
}