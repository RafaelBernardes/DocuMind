using DocuMind.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocuMind.Core.Tests.Configuration;

public sealed class ConfigurationRegistrationTests
{
    [Fact]
    public void ResolvingOptionsFailsWhenCriticalConfigurationIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postgres:Schema"] = "public",
                ["OpenAI:Endpoint"] = "https://api.openai.com/v1/",
                ["OpenAI:ChatModel"] = "gpt-4.1-mini",
                ["OpenAI:EmbeddingModel"] = "text-embedding-3-small",
                ["LocalStorage:BasePath"] = "storage",
                ["LocalStorage:UploadsPath"] = "uploads",
                ["LocalStorage:ProcessedPath"] = "processed",
                ["Ingestion:ChunkSize"] = "1200",
                ["Ingestion:ChunkOverlap"] = "200",
                ["Ingestion:MaxFileSizeMb"] = "25",
                ["Ingestion:AllowedExtensions:0"] = ".pdf",
                ["Query:TopK"] = "5",
                ["Query:MinScore"] = "0.7",
                ["Query:MaxContextChunks"] = "8"
            })
            .Build();

        services.AddLogging();
        services.AddDocuMindConfiguration(configuration);

        using var provider = services.BuildServiceProvider();

        var postgresException = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<PostgresOptions>>().Value);
        var openAiException = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<OpenAiOptions>>().Value);

        Assert.Contains("Postgres:ConnectionString is required.", postgresException.Failures);
        Assert.Contains("OpenAI:ApiKey is required.", openAiException.Failures);
    }

    [Fact]
    public void ResolvedOptionsAreTypedWhenConfigurationIsValid()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postgres:ConnectionString"] = "Host=localhost;Port=5432;Database=documind;Username=postgres;Password=postgres",
                ["Postgres:Schema"] = "public",
                ["OpenAI:Endpoint"] = "https://api.openai.com/v1/",
                ["OpenAI:ApiKey"] = "test-key",
                ["OpenAI:ChatModel"] = "gpt-4.1-mini",
                ["OpenAI:EmbeddingModel"] = "text-embedding-3-small",
                ["LocalStorage:BasePath"] = "storage",
                ["LocalStorage:UploadsPath"] = "uploads",
                ["LocalStorage:ProcessedPath"] = "processed",
                ["Ingestion:ChunkSize"] = "1200",
                ["Ingestion:ChunkOverlap"] = "200",
                ["Ingestion:MaxFileSizeMb"] = "25",
                ["Ingestion:AllowedExtensions:0"] = ".pdf",
                ["Query:TopK"] = "5",
                ["Query:MinScore"] = "0.7",
                ["Query:MaxContextChunks"] = "8"
            })
            .Build();

        services.AddDocuMindConfiguration(configuration);

        using var provider = services.BuildServiceProvider();

        var postgres = provider.GetRequiredService<IOptions<PostgresOptions>>().Value;
        var openAi = provider.GetRequiredService<IOptions<OpenAiOptions>>().Value;
        var storage = provider.GetRequiredService<IOptions<LocalStorageOptions>>().Value;
        var ingestion = provider.GetRequiredService<IOptions<IngestionOptions>>().Value;
        var query = provider.GetRequiredService<IOptions<QueryOptions>>().Value;

        Assert.Equal("public", postgres.Schema);
        Assert.Equal("gpt-4.1-mini", openAi.ChatModel);
        Assert.Equal("uploads", storage.UploadsPath);
        Assert.Equal(1200, ingestion.ChunkSize);
        Assert.Equal(5, query.TopK);
    }

    [Fact]
    public void LocalStorageValidator_ShouldRejectPathsThatRepeatBasePath()
    {
        var validator = new LocalStorageOptionsValidator();
        var options = new LocalStorageOptions
        {
            BasePath = "storage",
            UploadsPath = "storage\\uploads",
            ProcessedPath = "processed"
        };

        var result = validator.Validate(name: null, options);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);

        Assert.False(result.Succeeded);
        Assert.Contains("LocalStorage:UploadsPath must be a relative path under BasePath.", failures);
    }

    [Fact]
    public void LocalStorageValidator_ShouldRejectUnsafeBasePath()
    {
        var validator = new LocalStorageOptionsValidator();
        var options = new LocalStorageOptions
        {
            BasePath = "..\\storage",
            UploadsPath = "uploads",
            ProcessedPath = "processed"
        };

        var result = validator.Validate(name: null, options);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);

        Assert.False(result.Succeeded);
        Assert.Contains("LocalStorage:BasePath must be a safe relative path.", failures);
    }

    [Fact]
    public void LocalStorageValidator_ShouldRejectPathTraversalSegments()
    {
        var validator = new LocalStorageOptionsValidator();
        var options = new LocalStorageOptions
        {
            BasePath = "storage",
            UploadsPath = "../uploads",
            ProcessedPath = "..\\processed"
        };

        var result = validator.Validate(name: null, options);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);

        Assert.False(result.Succeeded);
        Assert.Contains("LocalStorage:UploadsPath must be a relative path under BasePath.", failures);
        Assert.Contains("LocalStorage:ProcessedPath must be a relative path under BasePath.", failures);
    }

    [Fact]
    public void LocalStorageValidator_ShouldAllowTrailingSlashes()
    {
        var validator = new LocalStorageOptionsValidator();
        var options = new LocalStorageOptions
        {
            BasePath = "storage/",
            UploadsPath = "uploads/",
            ProcessedPath = "processed/"
        };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void LocalStorageValidator_ShouldRejectOverlappingStorageDirectories()
    {
        var validator = new LocalStorageOptionsValidator();
        var options = new LocalStorageOptions
        {
            BasePath = "storage",
            UploadsPath = "uploads",
            ProcessedPath = "uploads/processed"
        };

        var result = validator.Validate(name: null, options);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);

        Assert.False(result.Succeeded);
        Assert.Contains("LocalStorage:UploadsPath and ProcessedPath must be separate non-overlapping directories.", failures);
    }

    [Fact]
    public void LocalStorageValidator_ShouldRejectAbsolutePaths()
    {
        var validator = new LocalStorageOptionsValidator();
        var options = new LocalStorageOptions
        {
            BasePath = "C:\\storage",
            UploadsPath = "C:\\uploads",
            ProcessedPath = "/processed"
        };

        var result = validator.Validate(name: null, options);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);

        Assert.False(result.Succeeded);
        Assert.Contains("LocalStorage:BasePath must be a safe relative path.", failures);
        Assert.Contains("LocalStorage:UploadsPath must be a relative path under BasePath.", failures);
        Assert.Contains("LocalStorage:ProcessedPath must be a relative path under BasePath.", failures);
    }

    [Fact]
    public void IngestionValidator_ShouldRejectNullAllowedExtensions()
    {
        var validator = new IngestionOptionsValidator();
        var options = new IngestionOptions
        {
            ChunkSize = 1200,
            ChunkOverlap = 200,
            MaxFileSizeMb = 25,
            AllowedExtensions = null!
        };

        var result = validator.Validate(name: null, options);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);

        Assert.False(result.Succeeded);
        Assert.Contains("Ingestion:AllowedExtensions must contain at least one extension.", failures);
    }

    [Fact]
    public void PostgresValidator_ShouldRejectNonPublicSchema()
    {
        var validator = new PostgresOptionsValidator();
        var options = new PostgresOptions
        {
            ConnectionString = "Host=localhost;Port=5432;Database=documind;Username=postgres;Password=postgres",
            Schema = "rag"
        };

        var result = validator.Validate(name: null, options);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);

        Assert.False(result.Succeeded);
        Assert.Contains("Postgres:Schema currently supports only 'public' in the MVP.", failures);
    }
}
