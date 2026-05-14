namespace DocuMind.Infrastructure.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public bool Enabled { get; set; }
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "documents.ingestion";
    public string QueueName { get; set; } = "documents.ingestion.requested";
    public string RoutingKey { get; set; } = "documents.uploaded";
    public string DeadLetterExchangeName { get; set; } = "documents.ingestion.dlx";
    public string DeadLetterQueueName { get; set; } = "documents.ingestion.requested.dlq";
    public string DeadLetterRoutingKey { get; set; } = "documents.uploaded.dlq";
    public ushort PrefetchCount { get; set; } = 1;
    public int PublishBatchSize { get; set; } = 20;
    public int PollIntervalSeconds { get; set; } = 2;
}
