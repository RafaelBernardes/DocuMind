namespace DocuMind.Infrastructure.TextExtraction;

internal static class TextExtractionNormalization
{
    public static string NormalizeNewlines(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
