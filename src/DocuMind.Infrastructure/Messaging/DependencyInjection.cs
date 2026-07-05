using DocuMind.Core.Documents;
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

        return services;
    }
}
