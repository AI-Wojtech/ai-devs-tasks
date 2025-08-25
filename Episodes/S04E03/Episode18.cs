public class Episode18 : EpisodeBase
{
    public override string Name => "S04E03 — Zewnętrzne źródła danych (Episode18)";
    public override string Description => "Przygotowanie uniwersalnego mechanizmu do poszukiwania informacji na stronach i odpowiedź na pytania.";

    private const string BaseUrl = "https://softo.ag3nts.org/";
    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";

    public override async Task RunAsync()
    {
        var centralaKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
        var questionsUrl = $"https://c3ntrala.ag3nts.org/data/{centralaKey}/softo.json";

        var httpService = new HttpService();
        var openAi = new OpenAIService();

        var questions = await httpService.GetJsonAsync<Dictionary<string, string>>(questionsUrl);
        var answers = new Dictionary<string, string>();

        foreach (var (id, question) in questions)
        {
            Console.WriteLine($"\n==== SZUKAM ODPOWIEDZI NA PYTANIE {id} ====");
            Console.WriteLine($"Pytanie: {question}");
            
            var visited = new HashSet<string>();
            string currentUrl = BaseUrl;
            string answer = null;

            for (int depth = 0; depth < 10; depth++)
            {
                if (visited.Contains(currentUrl)) 
                {
                    Console.WriteLine($"URL już odwiedzony: {currentUrl}");
                    break;
                }
                
                visited.Add(currentUrl);
                    Console.WriteLine($"Głębokość: {depth}, URL: {currentUrl}");

                try
                {
                    string content = await httpService.GetHtmlAsync(currentUrl);
                    string markdown = HtmlHelper.ConvertHtmlToMarkdown(content);

                    // DEBUGOWANIE: Pokaż fragment treści dla pytania o BanaN
                    if (question.Contains("BanaN") || question.Contains("BanAN"))
                    {
                        Console.WriteLine($"\n--- FRAGMENT TREŚCI DLA BanaN ---");
                        var lines = markdown.Split('\n');
                        var relevantLines = lines.Where(line => 
                            line.ToLowerInvariant().Contains("banan") || 
                            line.Contains("http") || 
                            line.Contains("interfejs") ||
                            line.Contains("robot") ||
                            line.Contains("sterowanie")).Take(10);
                        
                        foreach (var line in relevantLines)
                        {
                            Console.WriteLine($">> {line}");
                        }
                        Console.WriteLine("--- KONIEC FRAGMENTU ---\n");
                    }

                    string systemPromptCheck = @"
Jesteś ekspertem od analizy treści stron internetowych. Analizujesz treść strony w poszukiwaniu konkretnej odpowiedzi.

TWOJE ZADANIE:
Przeczytaj dokładnie treść strony i znajdź odpowiedź na pytanie użytkownika.

CO SZUKAĆ W ZALEŻNOŚCI OD PYTANIA:
1. Email firmy: szukaj adresów email w sekcjach kontakt, stopka, nagłówek
2. Adres interfejsu webowego do sterowania robotami dla BanAN: 
   - Szukaj URL-i zawierających 'banan' w domenie
   - Szukaj linków do demo, aplikacji, interfejsów
   - Może być w opisie projektu, w sekcji 'dostęp' lub 'link'
3. Certyfikaty ISO: szukaj oznaczeń 'ISO' z numerami (np. ISO 9001, ISO/IEC 27001)

ZASADY ODPOWIEDZI:
- Jeśli znajdziesz DOKŁADNĄ odpowiedź: podaj TYLKO tę informację (bez prefiksów, opisów)
- Jeśli nie ma odpowiedzi na tej stronie: odpowiedz 'BRAK_ODPOWIEDZI'
- Dla certyfikatów: podaj pełną nazwę z numerami (np. 'ISO 9001 oraz ISO/IEC 27001')
- Dla URL-i: podaj pełny adres URL (np. 'https://banan.ag3nts.org/')

PRZYKŁADY PRAWIDŁOWYCH ODPOWIEDZI:
Pytanie o email → kontakt@softoai.whatever
Pytanie o interfejs BanAN → https://banan.ag3nts.org/
Pytanie o certyfikaty → ISO 9001 oraz ISO/IEC 27001
";

                    string inputCheck = $"PYTANIE: {question}\n\nAKTUALNY URL: {currentUrl}\n\nTREŚĆ STRONY:\n{markdown}";

                    var potentialAnswer = await openAi.GetAnswerAsync(inputCheck, systemPromptCheck, "gpt-4o");
                    potentialAnswer = potentialAnswer?.Trim();

                    Console.WriteLine($"Odpowiedź LLM: {potentialAnswer}");

                    // Sprawdź czy to rzeczywista odpowiedź - bardziej restrykcyjna walidacja
                    if (!string.IsNullOrWhiteSpace(potentialAnswer) && 
                        !potentialAnswer.Equals("BRAK_ODPOWIEDZI", StringComparison.OrdinalIgnoreCase) &&
                        !potentialAnswer.ToLowerInvariant().Contains("nie mogę") &&
                        !potentialAnswer.ToLowerInvariant().Contains("brak") &&
                        !potentialAnswer.ToLowerInvariant().Contains("nie ma") &&
                        !potentialAnswer.ToLowerInvariant().Contains("nie znaleziono") &&
                        potentialAnswer.Length > 5 && // Zwiększone z 3 do 5
                        (potentialAnswer.Contains("@") || potentialAnswer.Contains("http") || potentialAnswer.Contains("ISO"))) // Dodatkowa walidacja formatu
                    {
                        answer = potentialAnswer;
                        Console.WriteLine($"Znaleziono odpowiedź: {answer}");
                        break;
                    }

                    // SYSTEM PROMPT: wybierz link
                    string systemPromptLink = @"
Analizujesz stronę internetową i wybierasz najlepszy link do znalezienia odpowiedzi na pytanie.

ALGORYTM WYBORU LINKU:
1. Przeczytaj pytanie i zidentyfikuj kluczowe słowa
2. Przeanalizuj wszystkie dostępne linki na stronie
3. Wybierz link najbardziej pasujący do tematu pytania

MAPOWANIE PYTAŃ NA LINKI:
- Email firmy → 'kontakt', 'contact', 'o-nas', 'about', 'footer'
- Projekty/realizacje/interfejsy → 'portfolio', 'projekty', 'realizacje', 'clients'
- Certyfikaty/jakość → 'certyfikaty', 'jakość', 'o-firmie', 'aktualności', 'blog', 'sukcesy'
- Konkretna firma (BanaN) → jeśli widzisz nazwę firmy w linku - wybierz go

SPECJALNE PRZYPADKI:
- Jeśli jesteś na stronie portfolio i pytanie dotyczy BanaN → szukaj linku z 'BanaN'
- Jeśli pytanie o certyfikaty a nie ma bezpośredniego linku → sprawdź 'aktualności', 'blog', 'news'
- Jeśli jesteś w sekcji aktualności/blog → szukaj artykułów o 'sukcesach', 'certyfikatach', 'osiągnięciach'

ODPOWIEDŹ:
Zwróć TYLKO jeden URL (względny lub bezwzględny) lub 'BRAK_LINKU'

PRZYKŁADY:
Pytanie o email na głównej → /kontakt
Pytanie o BanaN na portfolio → link do projektu BanaN
Pytanie o certyfikaty na głównej → /aktualnosci
Pytanie o certyfikaty w aktualnościach → link do artykułu o sukcesach
";

                    string inputLink = $"PYTANIE: {question}\n\nAKTUALNY URL: {currentUrl}\n\nTREŚĆ STRONY (ze wszystkimi linkami):\n{markdown}";

                    var nextLink = await openAi.GetAnswerAsync(inputLink, systemPromptLink, "gpt-4o");
                    nextLink = nextLink?.Trim();

                    Console.WriteLine($"Następny link: {nextLink}");
                    
                    // DEBUGOWANIE: Dodatkowe info dla BanaN
                    if (question.Contains("BanaN") || question.Contains("BanAN"))
                    {
                        Console.WriteLine($"DEBUGOWANIE BanaN - aktualny URL: {currentUrl}");
                        Console.WriteLine($"DEBUGOWANIE BanaN - sugerowany link: {nextLink}");
                        
                        // Pokaż wszystkie linki na stronie
                        var allLinks = markdown.Split('\n')
                            .Where(line => line.Contains("](") || line.Contains("href"))
                            .Take(20);
                        Console.WriteLine("Wszystkie dostępne linki:");
                        foreach (var link in allLinks)
                        {
                            Console.WriteLine($"  {link}");
                        }
                    }

                    if (string.IsNullOrWhiteSpace(nextLink) || 
                        nextLink.Equals("BRAK_LINKU", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Brak dalszych linków do sprawdzenia");
                        break;
                    }

                    // Normalizuj URL
                    if (!nextLink.StartsWith("http"))
                    {
                        if (nextLink.StartsWith("/"))
                            nextLink = BaseUrl.TrimEnd('/') + nextLink;
                        else
                            nextLink = new Uri(new Uri(currentUrl), nextLink).ToString();
                    }

                    currentUrl = nextLink;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd podczas przetwarzania {currentUrl}: {ex.Message}");
                    break;
                }
            }

            // Zapisz odpowiedź
            if (!string.IsNullOrWhiteSpace(answer))
            {
                answers[id] = answer;
            }
            else
            {
                answers[id] = "NIE ZNALEZIONO";
                Console.WriteLine($"Nie znaleziono odpowiedzi na pytanie {id}");
            }
        }

        // Wyświetl wszystkie odpowiedzi przed wysłaniem
        Console.WriteLine("\n=== FINALNE ODPOWIEDZI ===");
        foreach (var (id, answer) in answers)
        {
            Console.WriteLine($"{id}: {answer}");
        }

        var flg = await httpService.SendAnswerAsync(answers, "softo");
        Console.WriteLine($"Raport wysłany! {flg}");
    }
}