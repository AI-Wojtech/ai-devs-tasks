    public static class StringHelper
    {
        public static bool ContainsAny(this string source, string commaSeparatedKeywords)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(commaSeparatedKeywords))
                return false;

            var keywords = commaSeparatedKeywords
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return keywords.Any(keyword => source.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }
    }