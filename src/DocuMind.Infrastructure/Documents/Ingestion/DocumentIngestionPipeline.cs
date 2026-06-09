using DocuMind.Core.Documents;
using Microsoft.Extensions.Logging;

namespace DocuMind.Infrastructure.Documents.Ingestion;

public sealed class DocumentIngestionPipeline(
    ILogger<DocumentIngestionPipeline> logger) : IDocumentIngestionPipeline
{
    public Task ProcessAsync(DocumentIngestionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation(
            "Document ingestion pipeline contract invoked for DocumentId {DocumentId}. Processing orchestration will be implemented in a subsequent task.",
            request.DocumentId);

        return Task.CompletedTask;
    }
}
