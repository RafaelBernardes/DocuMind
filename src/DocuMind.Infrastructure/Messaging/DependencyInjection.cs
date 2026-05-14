using DocuMind.Infrastructure.Messaging.DocumentIngestion;
using DocuMind.Infrastructure.Messaging.Outbox;
using DocuMind.Infrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.Infrastructure.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddDocuMindMessaging(this IServiceCollection services)
    {
        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddSingleton<RabbitMqTopologyInitializer>();
        services.AddScoped<OutboxPublisherService>();
        services.AddSingleton<IOutboxMessagePublisher, RabbitMqOutboxMessagePublisher>();
        services.AddSingleton<IDocumentIngestionMessageHandler, DocumentIngestionNoOpMessageHandler>();

        return services;
    }
}
