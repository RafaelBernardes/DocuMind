using System.Text;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Messaging.Outbox;
using DocuMind.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DocuMind.Infrastructure.Messaging.RabbitMq;

internal sealed class RabbitMqOutboxMessagePublisher(
    RabbitMqConnectionFactory connectionFactory,
    RabbitMqTopologyInitializer topologyInitializer,
    IOptions<RabbitMqOptions> options) : IOutboxMessagePublisher
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task PublishAsync(OutboxMessageEntity message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        await topologyInitializer.InitializeAsync(cancellationToken);

        await using var connection = await connectionFactory.Create().CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.Id.ToString("N"),
            Type = message.Type,
            CorrelationId = message.DocumentId.ToString("N"),
            Timestamp = new AmqpTimestamp(message.OccurredAtUtc.ToUnixTimeSeconds())
        };

        var payload = Encoding.UTF8.GetBytes(message.Payload);

        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: payload,
            cancellationToken: cancellationToken);
    }
}
