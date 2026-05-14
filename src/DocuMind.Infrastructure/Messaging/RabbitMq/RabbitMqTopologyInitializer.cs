using DocuMind.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DocuMind.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqTopologyInitializer(
    RabbitMqConnectionFactory connectionFactory,
    IOptions<RabbitMqOptions> options)
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await connectionFactory.Create().CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var mainQueueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _options.DeadLetterExchangeName,
            ["x-dead-letter-routing-key"] = _options.DeadLetterRoutingKey
        };

        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _options.DeadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _options.QueueName,
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _options.DeadLetterQueueName,
            exchange: _options.DeadLetterExchangeName,
            routingKey: _options.DeadLetterRoutingKey,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}
