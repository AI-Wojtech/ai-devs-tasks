public class Episode07 : EpisodeBase
{
    public override string Name => "S02E02 — Video (Episode07)";
    public override string Description => "Znajdź ulicę instytutu profesora Maja na podstawie zdjęć map.";

    public override async Task RunAsync()
    {
        var openAI = new OpenAIService();

        var mapTilesFolder = @"D:\ai-dev\ai-devs-zadania-code\ai-devs-tasks\Episodes\S02E02\mapsTiles";
        var mapFiles = new[] { "map1.png", "map2.png", "map3.png", "map4.png" }
            .Select(file => Path.Combine(mapTilesFolder, file))
            .ToArray();

        // Budujemy listę zawartości: tekst + obrazy
        var contentList = new List<object>
        {
            new {
                type = "text",
                text = @"Jesteś ekspertem od analizy map i rozpoznawania lokalizacji. Twoim zadaniem jest określenie miast, z których pochodzą przedstawione fragmenty map. Prześlę Ci cztery fragmenty map (obrazy). Trzy z nich przedstawiają obszar tego samego miasta, natomiast jeden fragment pochodzi z innego miasta – jest to celowe wprowadzenie błędu.

Twoim celem jest:

Zidentyfikowanie miasta, które reprezentują trzy fragmenty map.
Wskazanie potencjalnego intruza - fragmentu mapy pochodzącego z innego miasta i wyjaśnienie, dlaczego uważasz, że on się wyróżnia.
Dokładna analiza każdego fragmentu mapy, w tym:
Nazwy ulic (jeśli są widoczne).
Charakterystyczne obiekty takie jak cmentarze, kościoły, szkoły, parki, rzeki, budynki użyteczności publicznej itp.
Ogólny układ urbanistyczny (np. czy jest to regularna siatka ulic, układ radialny, zabudowa historyczna).
Bardzo ważne: Przed ostatecznym określeniem miasta, upewnij się, że wszystkie zidentyfikowane przez Ciebie lokacje (ulice, obiekty) faktycznie znajdują się w tym mieście.  Sprawdź ich położenie i potwierdź ich obecność. Nie opieraj się tylko na pojedynczych elementach mapy – szukaj spójności i wzajemnych powiązań. 
Jako wynik podaj samą nazwę miasta.
" }
        };

        var prompt = @"Jesteś ekspertem od analizy map i rozpoznawania lokalizacji. Twoim zadaniem jest określenie miast, z których pochodzą przedstawione fragmenty map. Prześlę Ci cztery fragmenty map (obrazy). Trzy z nich przedstawiają obszar tego samego miasta, natomiast jeden fragment pochodzi z innego miasta – jest to celowe wprowadzenie błędu.

Twoim celem jest:

Zidentyfikowanie miasta, które reprezentują trzy fragmenty map.
Wskazanie potencjalnego intruza - fragmentu mapy pochodzącego z innego miasta i wyjaśnienie, dlaczego uważasz, że on się wyróżnia.
Dokładna analiza każdego fragmentu mapy, w tym:
Nazwy ulic (jeśli są widoczne).
Charakterystyczne obiekty takie jak cmentarze, kościoły, szkoły, parki, rzeki, budynki użyteczności publicznej itp.
Ogólny układ urbanistyczny (np. czy jest to regularna siatka ulic, układ radialny, zabudowa historyczna).
Bardzo ważne: Przed ostatecznym określeniem miasta, upewnij się, że wszystkie zidentyfikowane przez Ciebie lokacje (ulice, obiekty) faktycznie znajdują się w tym mieście.  Sprawdź ich położenie i potwierdź ich obecność. Nie opieraj się tylko na pojedynczych elementach mapy – szukaj spójności i wzajemnych powiązań. 
Jako wynik podaj samą nazwę miasta.
";


        foreach (var path in mapFiles)
        {
            var imageBytes = await File.ReadAllBytesAsync(path);
            var base64 = Convert.ToBase64String(imageBytes);
            contentList.Add(new
            {
                type = "image_url",
                image_url = new { url = $"data:image/png;base64,{base64}" }
            });
        }

        var request = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = contentList
                }
            },
            temperature = 0.2
        };

        string cityName = await openAI.GetAnswerWithImagesAsync(request);
        string cityName2 = await openAI.AnalyzeImagesWithPromptAsync(mapFiles, prompt);
        Console.WriteLine($"Zidentyfikowane miasto: {cityName}");
    }
}