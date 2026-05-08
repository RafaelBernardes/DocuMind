using DocuMind.Core.Documents;

namespace DocuMind.Infrastructure.TextExtraction;

internal interface ITextExtractionStrategy
{
    bool CanHandle(string extension);

    Task<TextExtractionResult> ExtractAsync(
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken);
}
