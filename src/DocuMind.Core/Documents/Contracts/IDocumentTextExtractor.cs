namespace DocuMind.Core.Documents;

public interface IDocumentTextExtractor
{
    Task<TextExtractionResult> ExtractAsync(
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken = default);
}
