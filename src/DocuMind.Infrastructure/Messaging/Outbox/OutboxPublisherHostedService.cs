using DocuMind.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.Infrastructure.Messaging.Outbox;

public sealed class OutboxPublisherHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<OutboxPublisherHostedService> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("RabbitMQ publishing is disabled. Outbox publisher will not start.");
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(_options.PollIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var publisherService = scope.ServiceProvider.GetRequiredService<OutboxPublisherService>();
                await publisherService.PublishPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected failure while publishing pending outbox messages.");
            }

            await Task.Delay(pollInterval, stoppingToken);
        }
    }
}
