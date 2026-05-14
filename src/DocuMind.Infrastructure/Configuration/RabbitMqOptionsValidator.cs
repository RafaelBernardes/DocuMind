using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Configuration;

public sealed class RabbitMqOptionsValidator : IValidateOptions<RabbitMqOptions>
{
    public ValidateOptionsResult Validate(string? name, RabbitMqOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.HostName))
        {
            return ValidateOptionsResult.Fail("RabbitMQ:HostName is required when RabbitMQ is enabled.");
        }

        if (options.Port <= 0)
        {
            return ValidateOptionsResult.Fail("RabbitMQ:Port must be greater than zero when RabbitMQ is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.ExchangeName))
        {
            return ValidateOptionsResult.Fail("RabbitMQ:ExchangeName is required when RabbitMQ is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.QueueName))
        {
            return ValidateOptionsResult.Fail("RabbitMQ:QueueName is required when RabbitMQ is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.RoutingKey))
        {
            return ValidateOptionsResult.Fail("RabbitMQ:RoutingKey is required when RabbitMQ is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.DeadLetterExchangeName))
        {
            return ValidateOptionsResult.Fail("RabbitMQ:DeadLetterExchangeName is required when RabbitMQ is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.DeadLetterQueueName))
        {
            return ValidateOptionsResult.Fail("RabbitMQ:DeadLetterQueueName is required when RabbitMQ is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.DeadLetterRoutingKey))
        {
            return ValidateOptionsResult.Fail("RabbitMQ:DeadLetterRoutingKey is required when RabbitMQ is enabled.");
        }

        if (options.PrefetchCount == 0)
        {
            return ValidateOptionsResult.Fail("RabbitMQ:PrefetchCount must be greater than zero when RabbitMQ is enabled.");
        }

        if (options.PublishBatchSize <= 0)
        {
            return ValidateOptionsResult.Fail("RabbitMQ:PublishBatchSize must be greater than zero when RabbitMQ is enabled.");
        }

        if (options.PollIntervalSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("RabbitMQ:PollIntervalSeconds must be greater than zero when RabbitMQ is enabled.");
        }

        return ValidateOptionsResult.Success;
    }
}
