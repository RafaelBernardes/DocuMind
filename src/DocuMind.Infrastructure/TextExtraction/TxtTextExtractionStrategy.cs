using System.Text;
using DocuMind.Core.Documents;

namespace DocuMind.Infrastructure.TextExtraction;

internal sealed class TxtTextExtractionStrategy : ITextExtractionStrategy
{
    public bool CanHandle(string extension)
    {
        return string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<TextExtractionResult> ExtractAsync(
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var text = await ReadAllTextAsync(content, cancellationToken);
        return string.IsNullOrWhiteSpace(text)
            ? TextExtractionResult.Failure(
                TextExtractionFailureCode.TextNotExtractable,
                $"File '{fileName}' does not contain extractable text.")
            : TextExtractionResult.Success(text);
    }

    private static async Task<string> ReadAllTextAsync(Stream content, CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            content.Seek(0, SeekOrigin.Begin);
        }

        using var reader = new StreamReader(
            content,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 8192,
            leaveOpen: true);

        var text = await reader.ReadToEndAsync(cancellationToken);
        return TextExtractionNormalization.NormalizeNewlines(text);
    }
}
