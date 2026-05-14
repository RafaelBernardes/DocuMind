using DocuMind.Core.Documents;

namespace DocuMind.Infrastructure.Documents.TextExtraction;

internal sealed class DocumentTextExtractor(IEnumerable<ITextExtractionStrategy> strategies) : IDocumentTextExtractor
{
    private readonly ITextExtractionStrategy[] _strategies = strategies?.ToArray()
        ?? throw new ArgumentNullException(nameof(strategies));

    public Task<TextExtractionResult> ExtractAsync(
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
        {
            throw new ArgumentException("Content stream must be readable.", nameof(content));
        }

        var extension = Path.GetExtension(fileName);
        var strategy = _strategies.SingleOrDefault(candidate => candidate.CanHandle(extension));

        return strategy is null
            ? Task.FromResult(TextExtractionResult.Failure(
                TextExtractionFailureCode.UnsupportedFileType,
                $"Files with extension '{extension}' are not supported for text extraction."))
            : strategy.ExtractAsync(fileName, contentType, content, cancellationToken);
    }
}
