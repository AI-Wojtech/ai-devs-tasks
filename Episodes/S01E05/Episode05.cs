using System.Text;
using System.Text.Json;

public class Episode05 : EpisodeBase
{
    public override string Name => "S01E05 — Produkcja";

    public override string Description => "Musisz przygotować system do cenzury danych agentów.";
    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";
    public override async Task RunAsync()
    {
        string apiKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
        string dataUrl = $"https://c3ntrala.ag3nts.org/data/{apiKey}/cenzura.txt";

        using var httpClient = new HttpClient();
        var originalText = await httpClient.GetStringAsync(dataUrl);

        string censoredText = await CensorSensitiveDataWithLLM(originalText);

        var payload = new ai_devs_tasks.Episodes.S01E05.FinalPayload
        {
            task = "CENZURA",
            apikey = apiKey,
            answer = censoredText
        };

        var httpService = new HttpService();
        var response = await httpService.PostJsonAsync<ai_devs_tasks.Episodes.S01E05.FinalPayload, object>(ReportUrl, payload);
    }

    private async Task<string> CensorSensitiveDataWithLLM(string input)
    {
        var prompt = $@"
        Twoim zadaniem jest ocenzurować dane osobowe w podanym tekście, **bez zmiany żadnych słów lub form gramatycznych oprócz cenzurowanych nazw**.

        - Imię i nazwisko (np. „Jan Nowak”) → „CENZURA” (bez odmiany)
        - Miasto i wszystkie jego odmiany przypadków (np. „Wrocław”, „we Wrocławiu”, „z Wrocławia”) → zawsze „CENZURA” (bez odmiany)
        - Ulica z numerem (np. „ul. Szeroka 18” lub „ulicy Lipowej 9”) → zamień nazwę ulicy i numer na „CENZURA”, ALE zachowaj dokładną formę słowa „ul.” lub „ulicy” bez żadnych zmian.  
        Przykłady zamiany:
        - „ul. Akacjowa 7” → „ul. CENZURA”
        - „ulicy Lipowej 9” → „ulicy CENZURA”
        - „ul. Konwaliowa 18” → „ul. CENZURA”
        - Wiek (np. „32 lata”, „Wiek: 26 lat”) → zastąp liczbę „CENZURA”, resztę pozostaw bez zmian, np.
            - „Ma 25 lat” → „Ma CENZURA lat”
            - „Wiek: 30 lat” → „Wiek: CENZURA lat”

        WAŻNE:
        - Nie zmieniaj słów typu „ul.” na „ulicy” ani odwrotnie.
        - Nie zamieniaj form gramatycznych ani nie dodawaj skrótów lub rozwinień.
        - Zachowaj oryginalną interpunkcję, spacje i szyk zdań.
        - Nie dodawaj żadnych komentarzy ani oznaczeń.
        - Nie odmieniać słowa „CENZURA” ani słowa „lat”.

        Przykłady:

        #Dobra odpowiedź:
        Input:
        Informacje o podejrzanym: Marek Jankowski. Mieszka w Białymstoku na ulicy Lipowej 9. Wiek: 26 lat.
        Output:
        Informacje o podejrzanym: CENZURA. Mieszka w CENZURA na ulicy CENZURA. Wiek: CENZURA lat.

        ---

        Tekst do przekształcenia:
        ---
        {input}
        ---
        Ocenzurowana wersja:
        ";

        var request = new
        {
            model = "mistral-7b-instruct-v0.3",
            messages = new[]
            {
            new { role = "user", content = prompt }
        },
            temperature = 0.1
        };

        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync("http://localhost:1234/v1/chat/completions", new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

        var responseText = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseText);

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content?.Trim() ?? throw new Exception("Model zwrócił pustą odpowiedź.");
    }

}