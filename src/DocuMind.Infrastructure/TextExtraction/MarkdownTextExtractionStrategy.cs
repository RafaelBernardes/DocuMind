using DocuMind.Core.Documents;

namespace DocuMind.Infrastructure.TextExtraction;

internal sealed class MarkdownTextExtractionStrategy : ITextExtractionStrategy
{
    private readonly TxtTextExtractionStrategy _reader = new();

    public bool CanHandle(string extension)
    {
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase);
    }

    public Task<TextExtractionResult> ExtractAsync(
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        return _reader.ExtractAsync(fileName, contentType, content, cancellationToken);
    }
}
