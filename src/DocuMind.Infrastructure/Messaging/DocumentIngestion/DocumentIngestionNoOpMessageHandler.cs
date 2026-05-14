using DocuMind.Core.Documents.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace DocuMind.Infrastructure.Messaging.DocumentIngestion;

internal sealed class DocumentIngestionNoOpMessageHandler(
    ILogger<DocumentIngestionNoOpMessageHandler> logger) : IDocumentIngestionMessageHandler
{
    public Task HandleAsync(DocumentUploadedMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Received document ingestion message for DocumentId {DocumentId}. The processing pipeline is not connected yet.",
            message.DocumentId);

        return Task.CompletedTask;
    }
}
