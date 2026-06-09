namespace DocuMind.Infrastructure.Messaging.RabbitMq;

public interface IRabbitMqTopologyInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
