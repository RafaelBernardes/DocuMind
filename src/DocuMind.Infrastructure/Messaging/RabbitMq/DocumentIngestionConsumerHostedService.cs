using System.Text;
using System.Text.Json;
using DocuMind.Core.Documents.IntegrationEvents;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Messaging.DocumentIngestion;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DocuMind.Infrastructure.Messaging.RabbitMq;

public sealed class DocumentIngestionConsumerHostedService(
    RabbitMqConnectionFactory connectionFactory,
    RabbitMqTopologyInitializer topologyInitializer,
    IDocumentIngestionMessageHandler handler,
    IOptions<RabbitMqOptions> options,
    ILogger<DocumentIngestionConsumerHostedService> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("RabbitMQ consumer is disabled. Document worker consumer will not start.");
            return;
        }

        await topologyInitializer.InitializeAsync(stoppingToken);

        await using var connection = await connectionFactory.Create().CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.BasicQosAsync(0, _options.PrefetchCount, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                var message = JsonSerializer.Deserialize<DocumentUploadedMessage>(body);
                if (message is null)
                {
                    throw new InvalidOperationException("Could not deserialize the document ingestion message.");
                }

                await handler.HandleAsync(message, stoppingToken);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to consume document ingestion message with delivery tag {DeliveryTag}.",
                    eventArgs.DeliveryTag);

                await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        var consumerTag = await channel.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await channel.BasicCancelAsync(consumerTag, false, CancellationToken.None);
        }
    }
}
