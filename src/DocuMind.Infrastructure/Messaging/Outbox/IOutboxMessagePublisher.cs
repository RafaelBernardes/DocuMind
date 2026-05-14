using DocuMind.Infrastructure.Persistence.Entities;

namespace DocuMind.Infrastructure.Messaging.Outbox;

public interface IOutboxMessagePublisher
{
    Task PublishAsync(OutboxMessageEntity message, CancellationToken cancellationToken = default);
}
