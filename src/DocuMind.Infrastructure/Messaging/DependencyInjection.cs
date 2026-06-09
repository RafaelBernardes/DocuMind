using DocuMind.Core.Documents;
using DocuMind.Infrastructure.Documents.Ingestion;
using DocuMind.Infrastructure.Messaging.DocumentIngestion;
using DocuMind.Infrastructure.Messaging.Outbox;
using DocuMind.Infrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.Infrastructure.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddDocuMindMessaging(this IServiceCollection services)
    {
        services.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();
        services.AddSingleton<RabbitMqTopologyInitializer>();
        services.AddSingleton<IRabbitMqTopologyInitializer>(serviceProvider =>
            serviceProvider.GetRequiredService<RabbitMqTopologyInitializer>());
        services.AddScoped<OutboxPublisherService>();
        services.AddSingleton<IOutboxMessagePublisher, RabbitMqOutboxMessagePublisher>();
        services.AddScoped<IDocumentIngestionPipeline, DocumentIngestionPipeline>();
        services.AddScoped<IDocumentIngestionMessageHandler, DocumentIngestionMessageHandler>();

        return services;
    }
}
