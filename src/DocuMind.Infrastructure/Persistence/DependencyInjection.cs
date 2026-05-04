using DocuMind.Core.Documents;
using DocuMind.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace DocuMind.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddDocuMindPersistence(
        this IServiceCollection services)
    {
        services.AddDbContext<DocuMindDbContext>((serviceProvider, options) =>
        {
            var postgresOptions = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<PostgresOptions>>()
                .Value;

            options.UseNpgsql(
                postgresOptions.ConnectionString,
                npgsqlOptions => npgsqlOptions.UseVector());
        });

        services.AddScoped<IDocumentRepository, DocumentRepository>();

        return services;
    }
}
