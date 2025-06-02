public class KeywordNormalizer
{
    private readonly OpenAIService _openAiService;
    private readonly Dictionary<string, string> _normalizationCache = new(StringComparer.OrdinalIgnoreCase);

    public KeywordNormalizer(OpenAIService openAiService)
    {
        _openAiService = openAiService;
    }

    public async Task<string> NormalizeAsync(string keyword)
    {
        if (_normalizationCache.TryGetValue(keyword, out var normalized))
        {
            return normalized;
        }

        string prompt = $$"""
            Jesteś ekspertem od analizy tekstów przemysłowych i incydentów w języku polskim.
            Twoim zadaniem jest znormalizowanie słowa kluczowego do najbardziej odpowiedniej, konkretnej formy w języku polskim, w mianowniku.
            - Zamień słowa angielskie lub inne formy na polskie odpowiedniki (np. "wildlife" → "zwierzęta", "capture" → "przechwycenie", "arrest" → "aresztowanie", "teacher" → "nauczyciel").
            - Jeśli słowo oznacza zawód, upewnij się, że jest precyzyjne (np. "developer" → "programista", "frontend developer" → "programista JavaScript").
            - Uwzględnij dowody kryminalistyczne (np. "fingerprints" → "odciski palców") i lokalizacje (np. "sector C4" → "sektor C4").
            - Słowo powinno być specyficzne dla kontekstu przemysłowego lub incydentów (np. "zamieszki", "sabotaż", "przechwycenie").
            - Zwróć TYLKO znormalizowane słowo, bez dodatkowych wyjaśnień.

            Przykłady:
            - "teacher" → "nauczyciel"
            - "capture" → "przechwycenie"
            - "arrest" → "aresztowanie"
            - "developer" → "programista"
            - "frontend developer" → "programista JavaScript"
            - "fingerprints" → "odciski palców"
            - "sector C4" → "sektor C4"
            - "evidence" → "dowody"

            Słowo kluczowe: {{keyword}}
            """;

        string normalizedKeyword = await _openAiService.GetAnswerAsync(prompt);
        normalizedKeyword = normalizedKeyword.Trim();
        _normalizationCache[keyword] = normalizedKeyword;
        return normalizedKeyword;
    }

    public async Task<List<string>> NormalizeListAsync(IEnumerable<string> keywords)
    {
        var normalizedKeywords = new List<string>();
        foreach (var keyword in keywords)
        {
            var normalized = await NormalizeAsync(keyword);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                normalizedKeywords.Add(normalized);
            }
        }
        return normalizedKeywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}