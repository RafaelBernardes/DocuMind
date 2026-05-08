using DocuMind.Core.Documents;
using DocuMind.Core.Storage;
using DocuMind.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocuMind.Core.Tests.Api;

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly InMemoryDocumentRepository _repository = new();
    private readonly InMemoryFileStorage _fileStorage = new();

    public InMemoryDocumentRepository Repository => _repository;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{PostgresOptions.SectionName}:ConnectionString"] = "Host=localhost;Database=documind_test;Username=test;Password=test",
                [$"{PostgresOptions.SectionName}:Schema"] = "public",
                [$"{OpenAiOptions.SectionName}:Endpoint"] = "https://api.openai.test",
                [$"{OpenAiOptions.SectionName}:ApiKey"] = "test-api-key",
                [$"{OpenAiOptions.SectionName}:ChatModel"] = "gpt-test",
                [$"{OpenAiOptions.SectionName}:EmbeddingModel"] = "text-embedding-test",
                [$"{LocalStorageOptions.SectionName}:BasePath"] = "storage",
                [$"{LocalStorageOptions.SectionName}:UploadsPath"] = "uploads",
                [$"{LocalStorageOptions.SectionName}:ProcessedPath"] = "processed",
                [$"{IngestionOptions.SectionName}:ChunkSize"] = "1000",
                [$"{IngestionOptions.SectionName}:ChunkOverlap"] = "100",
                [$"{IngestionOptions.SectionName}:MaxFileSizeMb"] = "25",
                [$"{IngestionOptions.SectionName}:AllowedExtensions:0"] = ".pdf",
                [$"{IngestionOptions.SectionName}:AllowedExtensions:1"] = ".md",
                [$"{IngestionOptions.SectionName}:AllowedExtensions:2"] = ".txt",
                [$"{QueryOptions.SectionName}:TopK"] = "5",
                [$"{QueryOptions.SectionName}:MinScore"] = "0.5",
                [$"{QueryOptions.SectionName}:MaxContextChunks"] = "8"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDocumentRepository>();
            services.RemoveAll<IFileStorage>();

            services.AddSingleton<IDocumentRepository>(_repository);
            services.AddSingleton<IFileStorage>(_fileStorage);
        });
    }
}

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly Dictionary<Guid, Document> _documents = [];
    private readonly object _syncRoot = new();

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            _documents.TryGetValue(id, out var document);
            return Task.FromResult(document);
        }
    }

    public Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        lock (_syncRoot)
        {
            _documents[document.Id] = document;
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        lock (_syncRoot)
        {
            _documents[document.Id] = document;
        }

        return Task.CompletedTask;
    }

    public void Seed(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        lock (_syncRoot)
        {
            _documents[document.Id] = document;
        }
    }
}

public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = [];
    private readonly object _syncRoot = new();

    public async Task<StoredFile> SaveUploadAsync(
        Guid documentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, cancellationToken);

        var relativePath = $"uploads/{documentId:N}/{Path.GetFileName(fileName)}";
        lock (_syncRoot)
        {
            _files[relativePath] = memoryStream.ToArray();
        }

        return new StoredFile(relativePath, memoryStream.Length);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (!_files.TryGetValue(relativePath, out var bytes))
            {
                throw new FileNotFoundException("Stored file was not found.", relativePath);
            }

            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
    }

    public Task<StoredFile> MoveToProcessedAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
