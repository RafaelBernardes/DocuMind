using DocuMind.Core.Documents;
using DocuMind.Infrastructure.Documents.Ingestion;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.Infrastructure.Messaging.DocumentIngestion;

public static class DependencyInjection
{
    public static IServiceCollection AddDocuMindWorkerDocumentIngestion(this IServiceCollection services)
    {
        services.AddScoped<IngestionFailurePolicy>();
        services.AddScoped<IDocumentIngestionPipeline, DocumentIngestionPipeline>();
        services.AddScoped<IDocumentIngestionMessageHandler, DocumentIngestionMessageHandler>();

        return services;
    }
}
