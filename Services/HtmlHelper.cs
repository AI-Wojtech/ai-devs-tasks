using ReverseMarkdown;
public static class HtmlHelper
{
    public static string ExtractQuestionE01(string html)
    {
        var start = html.IndexOf("<p id=\"human-question\">");
        if (start == -1) return "Nie znaleziono pytania";

        start += "<p id=\"human-question\">".Length;
        var end = html.IndexOf("</p>", start);

        var questionText = html.Substring(start, end - start).Trim();

        var questionStart = questionText.IndexOf("Question:");
        if (questionStart != -1)
        {
            questionText = questionText.Substring(questionStart + "Question:".Length).Trim();
        }

        questionText = System.Text.RegularExpressions.Regex.Replace(questionText, @"<br\s*/?>", " ").Trim();

        return questionText;
    }

    public static async Task<string> DownloadAndConvertHtmlToMarkdown(string url)
    {
        using var client = new HttpClient();
        var html = await client.GetStringAsync(url);

        var config = new Config
        {
            GithubFlavored = true,
            RemoveComments = true,
            SmartHrefHandling = true
        };

        var converter = new Converter(config);
        string markdown = converter.Convert(html);

        return markdown;
    }

    public class ImageWithCaption
    {
        public string Url { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
    }

    public static List<ImageWithCaption> ExtractImageUrls(string html, string baseUrl)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        var results = new List<ImageWithCaption>();
        var urls = new List<string>();

        // 1. Zwykłe <img src=...>
        var imgNodes = doc.DocumentNode.SelectNodes("//img[@src]");
        if (imgNodes != null)
        {
            urls.AddRange(imgNodes
                .Select(n => n.GetAttributeValue("src", ""))
                .Where(s => !string.IsNullOrEmpty(s)));
        }

        // 2. <img data-cfsrc=...>
        var dataSrcNodes = doc.DocumentNode.SelectNodes("//img[@data-cfsrc]");
        if (dataSrcNodes != null)
        {
            urls.AddRange(dataSrcNodes
                .Select(n => n.GetAttributeValue("data-cfsrc", ""))
                .Where(s => !string.IsNullOrEmpty(s)));
        }

        // 3. <noscript><img src=...></noscript>
        var noscriptNodes = doc.DocumentNode.SelectNodes("//noscript");
        if (noscriptNodes != null)
        {
            foreach (var node in noscriptNodes)
            {
                var innerHtml = node.InnerHtml;
                var subDoc = new HtmlAgilityPack.HtmlDocument();
                subDoc.LoadHtml(innerHtml);

                var innerImg = subDoc.DocumentNode.SelectSingleNode("//img[@src]");
                if (innerImg != null)
                {
                    var src = innerImg.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(src))
                        urls.Add(src);
                }
            }
        }

        // Teraz idziemy po <figure>, bo tam jest <img> i <figcaption>
        var figureNodes = doc.DocumentNode.SelectNodes("//figure");
        if (figureNodes != null)
        {
            foreach (var figure in figureNodes)
            {
                var imgNode = figure.SelectSingleNode(".//img[@src or @data-cfsrc]");
                if (imgNode == null)
                    continue;

                // Pobierz URL obrazu (daje priorytet data-cfsrc jeśli istnieje)
                var rawUrl = imgNode.GetAttributeValue("data-cfsrc",
                            imgNode.GetAttributeValue("src", ""));

                if (string.IsNullOrEmpty(rawUrl))
                    continue;

                // Dopasuj absolutny URL
                var absoluteUrl = new Uri(new Uri(baseUrl), rawUrl).ToString();

                // Pobierz figcaption (jeśli jest)
                var captionNode = figure.SelectSingleNode(".//figcaption");
                var caption = captionNode?.InnerText.Trim() ?? string.Empty;

                // Dodaj do wyników
                results.Add(new ImageWithCaption
                {
                    Url = absoluteUrl,
                    Caption = caption
                });

                // Usuń z listy urls, żeby nie duplikować obrazów z figure
                urls.Remove(rawUrl);
            }
        }

        // Dodaj pozostałe obrazy, które nie były wewnątrz <figure> (bez caption)
        foreach (var url in urls.Distinct())
        {
            var absoluteUrl = new Uri(new Uri(baseUrl), url).ToString();
            results.Add(new ImageWithCaption { Url = absoluteUrl, Caption = string.Empty });
        }

        return results;
    }


    public static List<string> ExtractAudioUrls(string html)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode
            .SelectNodes("//audio/source[@src]|//audio[@src]")?
            .Select(n => n.GetAttributeValue("src", ""))
            .Where(s => s.EndsWith(".mp3"))
            .ToList() ?? new();
    }

    public static async Task<string> DownloadHtmlAsync(string url)
    {
        using var client = new HttpClient();
        return await client.GetStringAsync(url);
    }

    public static string ConvertHtmlToMarkdown(string html)
    {
        var converter = new ReverseMarkdown.Converter();
        return converter.Convert(html);
    }

    private static readonly HttpClient _httpClient = new HttpClient();


    public static async Task<string> DownloadTextAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

}