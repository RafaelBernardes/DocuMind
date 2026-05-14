using DocuMind.Core.Documents.IntegrationEvents;

namespace DocuMind.Infrastructure.Messaging.DocumentIngestion;

public interface IDocumentIngestionMessageHandler
{
    Task HandleAsync(DocumentUploadedMessage message, CancellationToken cancellationToken = default);
}
