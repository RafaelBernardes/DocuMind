using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddDocuMindConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddValidatedOptions<PostgresOptions, PostgresOptionsValidator>(
            configuration.GetSection(PostgresOptions.SectionName));
        services.AddValidatedOptions<OpenAiOptions, OpenAiOptionsValidator>(
            configuration.GetSection(OpenAiOptions.SectionName));
        services.AddValidatedOptions<LocalStorageOptions, LocalStorageOptionsValidator>(
            configuration.GetSection(LocalStorageOptions.SectionName));
        services.AddValidatedOptions<IngestionOptions, IngestionOptionsValidator>(
            configuration.GetSection(IngestionOptions.SectionName));
        services.AddValidatedOptions<QueryOptions, QueryOptionsValidator>(
            configuration.GetSection(QueryOptions.SectionName));

        return services;
    }

    private static IServiceCollection AddValidatedOptions<TOptions, TValidator>(
        this IServiceCollection services,
        IConfigurationSection section)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
        services
            .AddOptions<TOptions>()
            .Bind(section)
            .ValidateOnStart();

        return services;
    }
}
