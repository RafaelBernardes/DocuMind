namespace DocuMind.Core.Documents;

public interface IDocumentIngestionPipeline
{
    Task ProcessAsync(DocumentIngestionRequest request, CancellationToken cancellationToken = default);
}
