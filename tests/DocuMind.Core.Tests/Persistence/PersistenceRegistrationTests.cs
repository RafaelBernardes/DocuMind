using DocuMind.Core.Documents;
using DocuMind.Core.Storage;
using DocuMind.Infrastructure.Chunking;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Embeddings;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Storage;
using DocuMind.Infrastructure.TextExtraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.Core.Tests.Persistence;

public sealed class PersistenceRegistrationTests
{
    [Fact]
    public void PersistenceServicesAreRegistered()
    {
        using var _ = new TestEnvironmentVariableScope(OpenAiOptions.ApiKeyEnvironmentVariableName, "test-key");

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postgres:ConnectionString"] = "Host=localhost;Port=5432;Database=documind;Username=postgres;Password=postgres",
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

        services.AddDocuMindConfiguration(configuration);
        services.AddDocuMindPersistence();
        services.AddDocuMindStorage();
        services.AddDocuMindChunking();
        services.AddDocuMindEmbeddings();
        services.AddDocuMindTextExtraction();

        using var provider = services.BuildServiceProvider();

        var dbContext = provider.GetRequiredService<DocuMindDbContext>();
        var repository = provider.GetRequiredService<IDocumentRepository>();
        var storage = provider.GetRequiredService<IFileStorage>();
        var chunker = provider.GetRequiredService<IDocumentChunker>();
        var embeddingClient = provider.GetRequiredService<IEmbeddingClient>();
        var textExtractor = provider.GetRequiredService<IDocumentTextExtractor>();

        Assert.NotNull(dbContext);
        Assert.NotNull(repository);
        Assert.NotNull(storage);
        Assert.NotNull(chunker);
        Assert.NotNull(embeddingClient);
        Assert.NotNull(textExtractor);
    }
}
