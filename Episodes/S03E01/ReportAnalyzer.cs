using System.Text.Json;

public class ReportAnalyzer
{
    private readonly string _reportsDirectory;
    private readonly string _factsDirectory;
    private readonly OpenAIService _openAiService;
    private readonly KeywordNormalizer _keywordNormalizer;

    private List<FactModel> _factCache = new();
    private List<ReportModel> _reportCache = new();

    public ReportAnalyzer(string reportsDirectory, string factsDirectory, OpenAIService openAiService)
    {
        _reportsDirectory = reportsDirectory;
        _factsDirectory = factsDirectory;
        _openAiService = openAiService;
        _keywordNormalizer = new KeywordNormalizer(openAiService);
    }

    public async Task<Dictionary<string, List<string>>> AnalyzeAsync()
    {
        await LoadAndAnalyzeFactsAsync();
        await LoadAndAnalyzeReportsAsync();
        return BuildFinalKeywordDictionary();
    }

    private async Task LoadAndAnalyzeFactsAsync()
    {
        var factFiles = Directory.GetFiles(_factsDirectory, "*.txt");

        foreach (var path in factFiles)
        {
            string content = await File.ReadAllTextAsync(path);

            // Prompt dla faktów z $$ dla poprawnego formatowania JSON
            string prompt = $$"""
                Jesteś ekspertem od analizy tekstów przemysłowych i incydentów w języku polskim.
                Na podstawie poniższej treści faktu wyodrębnij następujące informacje:
                - Imiona i nazwiska osób (w mianowniku, oddzielone przecinkami, np. "Jan Kowalski").
                - Zawody osób (w mianowniku, oddzielone przecinkami, np. "nauczyciel, programista JavaScript"). Zidentyfikuj wyraźnie zawody związane z programowaniem, np. "programista JavaScript", "frontend developer".
                - Specjalne umiejętności osób (w mianowniku, oddzielone przecinkami, np. "programowanie JavaScript").
                - Nazwy miejsc (w mianowniku, oddzielone przecinkami, np. "Warszawa, Grudziądz").
                - Słowa kluczowe opisujące treść (w mianowniku, oddzielone przecinkami, konkretne, np. "zwierzęta" zamiast "wildlife", "przechwycenie" zamiast "capture", "odciski palców" dla dowodów kryminalistycznych).

                Przykłady:
                - "teacher" → "nauczyciel"
                - "capture" → "przechwycenie"
                - "arrest" → "aresztowanie"
                - "developer" → "programista"
                - "frontend developer" → "programista JavaScript"
                - "fingerprints" → "odciski palców"

                Treść faktu:
                {{content}}

                Zwróć odpowiedź w formacie JSON:
                {
                    "persons": "imię1 nazwisko1, imię2 nazwisko2",
                    "professions": "zawód1, zawód2",
                    "skills": "umiejętność1, umiejętność2",
                    "places": "miejsce1, miejsce2",
                    "keywords": "słowo1, słowo2"
                }
                """;

            string response = await _openAiService.GetAnswerAsync(prompt);

            var jsonResponse = JsonDocument.Parse(response);
            var rawKeywords = ParseJsonField(jsonResponse, "keywords");
            var normalizedKeywords = await _keywordNormalizer.NormalizeListAsync(rawKeywords);

            var fact = new FactModel
            {
                Content = content,
                Persons = ParseJsonField(jsonResponse, "persons"),
                Professions = ParseJsonField(jsonResponse, "professions"),
                Skills = ParseJsonField(jsonResponse, "skills"),
                Places = ParseJsonField(jsonResponse, "places"),
                Keywords = normalizedKeywords
            };

            _factCache.Add(fact);

            Console.WriteLine($"Fakt: {Path.GetFileName(path)}");
            Console.WriteLine($"Treść: {content}");
            Console.WriteLine($"Osoby: {string.Join(", ", fact.Persons)}");
            Console.WriteLine($"Zawody: {string.Join(", ", fact.Professions)}");
            Console.WriteLine($"Umiejętności: {string.Join(", ", fact.Skills)}");
            Console.WriteLine($"Miejsca: {string.Join(", ", fact.Places)}");
            Console.WriteLine($"Słowa kluczowe: {string.Join(", ", fact.Keywords)}");
        }
    }

    private async Task LoadAndAnalyzeReportsAsync()
    {
        var reportFiles = Directory.GetFiles(_reportsDirectory, "*.txt", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f).Contains("report"))
            .OrderBy(f => f)
            .ToList();

        foreach (var path in reportFiles)
        {
            string content = await File.ReadAllTextAsync(path);
            string fileName = Path.GetFileName(path);

            string sector = fileName.Contains("sektor_") ? fileName.Split("sektor_")[1].Split('.')[0] : "nieznany sektor";

            var relatedFacts = _factCache
                .Where(f => ContainsAnyKeyword(content, f.Keywords) ||
                            f.Persons.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase)) ||
                            f.Places.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase)) ||
                            f.Professions.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase)) ||
                            (f.Professions.Any(p => p.Equals("nauczyciel", StringComparison.OrdinalIgnoreCase)) &&
                             (content.Contains("opór", StringComparison.OrdinalIgnoreCase) ||
                              content.Contains("ruch oporu", StringComparison.OrdinalIgnoreCase) ||
                              content.Contains("sabotaż", StringComparison.OrdinalIgnoreCase) ||
                              content.Contains("zamieszki", StringComparison.OrdinalIgnoreCase))) ||
                            (f.Professions.Any(p => p.Contains("programista", StringComparison.OrdinalIgnoreCase) ||
                                                    p.Contains("developer", StringComparison.OrdinalIgnoreCase)) &&
                             (content.Contains("ruch oporu", StringComparison.OrdinalIgnoreCase) ||
                              content.Contains("sabotaż", StringComparison.OrdinalIgnoreCase) ||
                              content.Contains("zamieszki", StringComparison.OrdinalIgnoreCase) ||
                              content.Contains("sztuczna inteligencja", StringComparison.OrdinalIgnoreCase))) ||
                            (f.Persons.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase)) &&
                             (content.Contains("odciski palców", StringComparison.OrdinalIgnoreCase) ||
                              content.Contains("śledztwo", StringComparison.OrdinalIgnoreCase) ||
                              content.Contains("dowody", StringComparison.OrdinalIgnoreCase))))
                .ToList();

            // Prompt dla raportów z $$ dla poprawnego formatowania JSON
            string prompt = $$"""
                Jesteś ekspertem od analizy incydentów przemysłowych w języku polskim.
                Na podstawie treści raportu, jego nazwy oraz powiązanych faktów wykonaj następujące kroki:
                1. Wygeneruj wstępne słowa kluczowe na podstawie treści raportu i nazwy pliku (w mianowniku, po polsku, oddzielone przecinkami, konkretne, np. "zwierzęta" zamiast "wildlife", "przechwycenie" zamiast "capture", "programista JavaScript" dla osób związanych z JavaScript, "odciski palców" dla dowodów kryminalistycznych).
                2. Zidentyfikuj osoby (imiona i nazwiska) wspomniane w raporcie (w mianowniku, oddzielone przecinkami).
                3. Zidentyfikuj miejsca wspomniane w raporcie (w mianowniku, oddzielone przecinkami), w tym sektor (np. "sektor C4").
                4. Zidentyfikuj kluczowe wydarzenia, takie jak "przechwycenie", "aresztowanie", "sabotaż", "śledztwo", "odciski palców" (w mianowniku, oddzielone przecinkami). Uwzględnij incydenty związane z ruchem oporu, programistami JavaScript lub dowodami kryminalistycznymi.

                Przykłady:
                - "teacher" → "nauczyciel"
                - "capture" → "przechwycenie"
                - "arrest" → "aresztowanie"
                - "developer" → "programista"
                - "frontend developer" → "programista JavaScript"
                - "fingerprints" → "odciski palców"
                - "sector C4" → "sektor C4"

                Nazwa pliku: {{fileName}}
                Treść raportu:
                {{content}}
                {{(relatedFacts.Any() ? "\nPowiązane fakty:\n" + string.Join("\n", relatedFacts.Select(f => $"- {f.Content}")) : "")}}

                Zwróć odpowiedź w formacie JSON:
                {
                    "preliminary_keywords": "słowo1, słowo2",
                    "persons": "imię1 nazwisko1, imię2 nazwisko2",
                    "places": "miejsce1, miejsce2",
                    "events": "zdarzenie1, zdarzenie2"
                }
                """;

            string response = await _openAiService.GetAnswerAsync(prompt);

            // Parsowanie odpowiedzi JSON
            var jsonResponse = JsonDocument.Parse(response);
            var preliminaryKeywords = ParseJsonField(jsonResponse, "preliminary_keywords");
            var persons = ParseJsonField(jsonResponse, "persons");
            var places = ParseJsonField(jsonResponse, "places");
            var events = ParseJsonField(jsonResponse, "events");

            // Normalizacja słów kluczowych
            var normalizedPreliminaryKeywords = await _keywordNormalizer.NormalizeListAsync(preliminaryKeywords);
            var normalizedEvents = await _keywordNormalizer.NormalizeListAsync(events);
            var relatedFactKeywords = await _keywordNormalizer.NormalizeListAsync(relatedFacts.SelectMany(f => f.Keywords).Distinct());
            var relatedFactProfessions = await _keywordNormalizer.NormalizeListAsync(relatedFacts.SelectMany(f => f.Professions).Distinct());
            var finalKeywords = normalizedPreliminaryKeywords
                .Concat(normalizedEvents)
                .Concat(relatedFactKeywords)
                .Concat(relatedFactProfessions)
                .Concat(persons)
                .Concat(places)
                .Concat(new[] { $"sektor {sector}" })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToList();

            _reportCache.Add(new ReportModel
            {
                FileName = fileName,
                Content = content,
                Keywords = finalKeywords,
                Persons = persons,
                Places = places,
                Events = events
            });

            Console.WriteLine($"Raport: {fileName}");
            Console.WriteLine($"Sektor: {sector}");
            Console.WriteLine($"Treść: {content}");
            Console.WriteLine($"Osoby: {string.Join(", ", persons)}");
            Console.WriteLine($"Miejsca: {string.Join(", ", places)}");
            Console.WriteLine($"Zawody w faktach: {string.Join(", ", relatedFacts.SelectMany(f => f.Professions).Distinct())}");
            Console.WriteLine($"Zdarzenia: {string.Join(", ", events)}");
            Console.WriteLine($"Powiązane fakty: {string.Join(", ", relatedFacts.Select(f => Path.GetFileName(f.Content)))}");
            Console.WriteLine($"Słowa kluczowe: {string.Join(", ", finalKeywords)}");
        }
    }

    private Dictionary<string, List<string>> BuildFinalKeywordDictionary()
    {
        return _reportCache.ToDictionary(
            r => r.FileName,
            r => r.Keywords
        );
    }

    private bool ContainsAnyKeyword(string content, List<string> keywords)
    {
        return keywords.Any(k => content.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private List<string> ParseJsonField(JsonDocument json, string fieldName)
    {
        if (json.RootElement.TryGetProperty(fieldName, out var field) && field.ValueKind == JsonValueKind.String)
        {
            return field.GetString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();
        }
        return new List<string>();
    }
}