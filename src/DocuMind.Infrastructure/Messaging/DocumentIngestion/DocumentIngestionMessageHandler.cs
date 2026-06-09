using DocuMind.Core.Documents;
using DocuMind.Core.Documents.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace DocuMind.Infrastructure.Messaging.DocumentIngestion;

internal sealed class DocumentIngestionMessageHandler(
    IDocumentIngestionPipeline ingestionPipeline,
    ILogger<DocumentIngestionMessageHandler> logger) : IDocumentIngestionMessageHandler
{
    public async Task HandleAsync(DocumentUploadedMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var request = new DocumentIngestionRequest(
            message.DocumentId,
            message.FileName,
            message.ContentType,
            message.SizeInBytes,
            message.StorageRelativePath,
            message.UploadedAtUtc);

        logger.LogInformation(
            "Received document ingestion message for DocumentId {DocumentId}. Delegating to the ingestion pipeline.",
            request.DocumentId);

        try
        {
            await ingestionPipeline.ProcessAsync(request, cancellationToken);

            logger.LogInformation(
                "Document ingestion pipeline completed for DocumentId {DocumentId}.",
                request.DocumentId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Document ingestion pipeline failed for DocumentId {DocumentId}.",
                request.DocumentId);

            throw;
        }
    }
}
