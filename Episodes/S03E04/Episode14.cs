public class Episode14 : EpisodeBase
{
    public override string Name => "S03E04 — Wyszukiwanie hybrydowe (Episode14)";
    public override string Description => "Centrala wystawiła dla Ciebie specjalne API, które umożliwi Ci wykonanie niemal dowolnych zapytań wyciągających dane ze wspomnianej bazy. Wiemy, że znajdują się tam tabele o nazwach users, datacenters oraz connections. Niekoniecznie potrzebujesz teraz wszystkich z nich. Twoim zadaniem jest zwrócenie nam numerów ID czynnych datacenter, które zarządzane są przez menadżerów, którzy aktualnie przebywają na urlopie (są nieaktywni). To pozwoli nam lepiej wytypować centra danych bardziej podatne na atak. Nazwa zadania to database.";

    private const string PeopleUrl = "https://c3ntrala.ag3nts.org/people";
    private const string PlacesUrl = "https://c3ntrala.ag3nts.org/places";
    private const string DataUrl = "https://c3ntrala.ag3nts.org/dane/barbara.txt";

    public class ResponseModel
    {
        public int Code { get; set; }
        public string Message { get; set; }
    }

    public override async Task RunAsync()
    {
        var ApiKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
        var httpService = new HttpService();
        using var client = new HttpClient();
        var openAiService = new OpenAIService();

        var noteContent = await client.GetStringAsync(DataUrl);

        var systemPrompt = @"Z podanego tekstu wyodrębnij:
1. Wszystkie imiona osób (tylko imiona, bez nazwisk), w mianowniku, BEZ polskich znaków. Usuń powtórzenia.
2. Wszystkie nazwy miast - wielkimi literami, BEZ polskich znaków. Ignoruj skróty.

Zwróć odpowiedź jako:
Imiona: [IMIE1, IMIE2, ...]  
Miasta: [MIASTO1, MIASTO2, ...]

Tekst:";
        var extractedData = await openAiService.GetAnswerAsync(noteContent, systemPrompt, "gpt-4.1");

        ParseExtractedData(extractedData, out Queue<string> namesQueue, out Queue<string> citiesQueue);

        var visitedNames = new HashSet<string>(namesQueue);
        var visitedCities = new HashSet<string>(citiesQueue);
        var knownBarbaraCities = new HashSet<string>(visitedCities);
        var potentialBarbaraCities = new List<string>();

        while (namesQueue.Count > 0 || citiesQueue.Count > 0)
        {
            if (namesQueue.Count > 0)
            {
                var name = namesQueue.Dequeue();

                try
                {
                    var response = await httpService.PostJsonAsync<ResponseModel>(PeopleUrl, new { apikey = ApiKey, query = name });
                    var newCities = ExtractTokens(response.Message);
                    foreach (var city in newCities)
                    {
                        if (visitedCities.Add(city))
                            citiesQueue.Enqueue(city);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd /people: {ex.Message}");
                }
            }

            if (citiesQueue.Count > 0)
            {
                var city = citiesQueue.Dequeue();

                try
                {
                    var response = await httpService.PostJsonAsync<ResponseModel>(PlacesUrl, new { apikey = ApiKey, query = city });
                    var newNames = ExtractTokens(response.Message);

                    foreach (var person in newNames)
                    {
                        if (person == "BARBARA" && !knownBarbaraCities.Contains(city))
                        {
                            potentialBarbaraCities.Add(city);
                        }
                        else if (visitedNames.Add(person))
                            namesQueue.Enqueue(person);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd /places: {ex.Message}");
                }
            }
        }

        if (potentialBarbaraCities.Any())
        {
            var answer = potentialBarbaraCities.First();
            try
            {
                var report = await httpService.SendAnswerAsync(answer, "loop");
                Console.WriteLine($"Odpowiedź wysłana: {report}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd zgłoszenia: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Nie znaleziono aktualnej lokalizacji Barbary.");
        }
    }

    private static void ParseExtractedData(string extracted, out Queue<string> namesQueue, out Queue<string> citiesQueue)
    {
        namesQueue = new Queue<string>();
        citiesQueue = new Queue<string>();

        foreach (var line in extracted.Split('\n'))
        {
            if (line.StartsWith("Imiona:", StringComparison.OrdinalIgnoreCase))
            {
                var raw = line.Replace("Imiona:", "").Replace("[", "").Replace("]", "").Trim();
                foreach (var name in raw.Split(','))
                    namesQueue.Enqueue(name.Trim().ToUpper());
            }
            else if (line.StartsWith("Miasta:", StringComparison.OrdinalIgnoreCase))
            {
                var raw = line.Replace("Miasta:", "").Replace("[", "").Replace("]", "").Trim();
                foreach (var city in raw.Split(','))
                    citiesQueue.Enqueue(city.Trim().ToUpper());
            }
        }
    }

    private static List<string> ExtractTokens(string raw)
    {
        if (raw.Contains("RESTRICTED")) return new List<string>();
        return raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                  .Select(s => s.Trim().ToUpper())
                  .ToList();
    }



}