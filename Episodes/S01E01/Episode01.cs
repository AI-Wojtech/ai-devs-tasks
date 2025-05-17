public class Episode01 : EpisodeBase
{
    public override string Name => "S01E01 — Interakcja z dużym modelem językowym";
    public override string Description => "Zautomatyzuj logowanie do systemu robotów na stronie https://xyz.ag3nts.org/, korzystając z podanych danych dostępowych (login: tester, hasło: 574e112a). Logowanie wymaga jeszcze odpowiedzi na pytanie, które pojawia się na stronie i co kilka sekund się zmienia. Twoja aplikacja powinna pobierać aktualne pytanie z tej strony – najprościej, jak się da, czyli wystarczy pobrać HTML strony i wyciągnąć z niego pytanie, np. przez regex lub zwykłe wyszukiwanie tekstu.";

    public override async Task RunAsync()
    {
        using (var client = new HttpClient())
        {
            string url = "https://xyz.ag3nts.org/";
            string html = await client.GetStringAsync(url);
            string question = HtmlHelper.ExtractQuestionE01(html);
            Console.WriteLine("Question: " + question);

            string prePrompt = "Odpowiadaj na pytania jedynie jedną, krótką odpowiedzią. Nie podawaj dodatkowych informacji. Jeśli pytanie jest o datę, podaj tylko rok. Pytanie na które masz odpowiedzieć to: ";

            string answer = await new OpenAIService().GetAnswerAsync(prePrompt + question);
            Console.WriteLine("Answer: " + answer);
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", "tester"),
                new KeyValuePair<string, string>("password", "574e112a"),
                new KeyValuePair<string, string>("answer", answer)
            });

            var message = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead);
            if (response != null)
            {
                Console.WriteLine("Response URL :" + response?.RequestMessage?.RequestUri?.OriginalString);
            }
            else
            {
                Console.WriteLine("Request uri not found");
            }
        }
    }
}