using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Messaging.Outbox;

public sealed class OutboxPublisherService(
    DocuMindDbContext dbContext,
    IOutboxMessagePublisher publisher,
    IOptions<RabbitMqOptions> options,
    ILogger<OutboxPublisherService> logger)
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task<int> PublishPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return 0;
        }

        var messages = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(_options.PublishBatchSize)
            .ToListAsync(cancellationToken);

        var publishedCount = 0;
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.ProcessedAtUtc = DateTimeOffset.UtcNow;
                message.Error = null;
                publishedCount++;
            }
            catch (Exception exception)
            {
                message.Error = exception.Message;
                logger.LogWarning(
                    exception,
                    "Failed to publish outbox message {OutboxMessageId} for DocumentId {DocumentId}.",
                    message.Id,
                    message.DocumentId);
            }
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return publishedCount;
    }
}
