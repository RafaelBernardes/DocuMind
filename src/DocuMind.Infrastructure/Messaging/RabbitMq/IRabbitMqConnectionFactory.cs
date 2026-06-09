using RabbitMQ.Client;

namespace DocuMind.Infrastructure.Messaging.RabbitMq;

public interface IRabbitMqConnectionFactory
{
    ValueTask<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
