using DocuMind.Core.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.Infrastructure.Chunking;

public static class DependencyInjection
{
    public static IServiceCollection AddDocuMindChunking(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentChunker, SimpleDeterministicDocumentChunker>();

        return services;
    }
}
