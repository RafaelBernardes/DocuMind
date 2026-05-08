using DocuMind.Core.Documents;
using UglyToad.PdfPig;

namespace DocuMind.Infrastructure.TextExtraction;

internal sealed class PdfTextExtractionStrategy : ITextExtractionStrategy
{
    public bool CanHandle(string extension)
    {
        return string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public Task<TextExtractionResult> ExtractAsync(
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (content.CanSeek)
            {
                content.Seek(0, SeekOrigin.Begin);
            }

            using var document = PdfDocument.Open(content);
            var pageTexts = document
                .GetPages()
                .Select(page => page.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(TextExtractionNormalization.NormalizeNewlines)
                .ToArray();

            if (pageTexts.Length == 0)
            {
                return Task.FromResult(TextExtractionResult.Failure(
                    TextExtractionFailureCode.TextNotExtractable,
                    $"PDF '{fileName}' does not contain extractable text."));
            }

            var text = string.Join("\n", pageTexts).Trim();
            return Task.FromResult(TextExtractionResult.Success(text));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(TextExtractionResult.Failure(
                TextExtractionFailureCode.InvalidContent,
                $"PDF '{fileName}' could not be parsed safely."));
        }
    }
}
