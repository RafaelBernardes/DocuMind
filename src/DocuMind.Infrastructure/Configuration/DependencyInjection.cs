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
        services.AddOpenAiOptions(configuration);
        services.AddValidatedOptions<LocalStorageOptions, LocalStorageOptionsValidator>(
            configuration.GetSection(LocalStorageOptions.SectionName));
        services.AddValidatedOptions<IngestionOptions, IngestionOptionsValidator>(
            configuration.GetSection(IngestionOptions.SectionName));
        services.AddValidatedOptions<QueryOptions, QueryOptionsValidator>(
            configuration.GetSection(QueryOptions.SectionName));

        return services;
    }

    private static IServiceCollection AddOpenAiOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<OpenAiOptions>, OpenAiOptionsValidator>();
        services
            .AddOptions<OpenAiOptions>()
            .Bind(configuration.GetSection(OpenAiOptions.SectionName))
            .Configure(options =>
            {
                options.ApiKey = ResolveOpenAiApiKeyFromEnvironment();
            })
            .ValidateOnStart();

        return services;
    }

    private static string ResolveOpenAiApiKeyFromEnvironment()
    {
        return FirstNonEmpty(
            Environment.GetEnvironmentVariable(OpenAiOptions.ApiKeyEnvironmentVariableName))
            ?? string.Empty;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
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
