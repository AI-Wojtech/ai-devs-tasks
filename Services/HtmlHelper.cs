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


}