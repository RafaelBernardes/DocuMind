using DocuMind.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.Infrastructure.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddDocuMindStorage(this IServiceCollection services)
    {
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        return services;
    }
}
