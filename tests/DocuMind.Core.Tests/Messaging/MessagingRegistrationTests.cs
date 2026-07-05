using DocuMind.Core.Documents;
using DocuMind.Core.Storage;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Messaging;
using DocuMind.Infrastructure.Messaging.DocumentIngestion;
using DocuMind.Infrastructure.Messaging.Outbox;
using DocuMind.Infrastructure.Messaging.RabbitMq;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocuMind.Core.Tests.Messaging;

public sealed class MessagingRegistrationTests
{
    [Fact]
    public void AddDocuMindMessaging_RegistersSharedMessagingWithoutDocumentIngestionServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<RabbitMqOptions>(_ => { });
        services.Configure<PostgresOptions>(options =>
        {
            options.ConnectionString = "Host=localhost;Database=documind;Username=postgres;Password=postgres";
            options.Schema = "public";
        });
        var dbContextOptions = new DbContextOptionsBuilder<DocuMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        services.AddSingleton(dbContextOptions);
        services.AddScoped<DocuMindDbContext>(serviceProvider =>
            new TestDocuMindDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<DocuMindDbContext>>(),
                serviceProvider.GetRequiredService<IOptions<PostgresOptions>>()));
        services.AddDocuMindMessaging();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        var connectionFactory = scope.ServiceProvider.GetRequiredService<IRabbitMqConnectionFactory>();
        var topologyInitializer = scope.ServiceProvider.GetRequiredService<IRabbitMqTopologyInitializer>();
        var outboxPublisher = scope.ServiceProvider.GetRequiredService<IOutboxMessagePublisher>();
        var outboxService = scope.ServiceProvider.GetRequiredService<OutboxPublisherService>();

        Assert.NotNull(connectionFactory);
        Assert.NotNull(topologyInitializer);
        Assert.NotNull(outboxPublisher);
        Assert.NotNull(outboxService);
        Assert.Null(scope.ServiceProvider.GetService<IDocumentIngestionPipeline>());
        Assert.Null(scope.ServiceProvider.GetService<IDocumentIngestionMessageHandler>());
    }

    [Fact]
    public void AddDocuMindWorkerDocumentIngestion_RegistersPipelineAndHandlerWhenDependenciesAreProvided()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IDocumentRepository>(_ => new FakeDocumentRepository());
        services.AddSingleton<IFileStorage, FakeFileStorage>();
        services.AddSingleton<IDocumentTextExtractor, FakeDocumentTextExtractor>();
        services.AddSingleton<IDocumentChunker, FakeDocumentChunker>();
        services.AddSingleton<IEmbeddingClient, FakeEmbeddingClient>();
        services.AddDocuMindWorkerDocumentIngestion();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IDocumentIngestionPipeline>();
        var handler = scope.ServiceProvider.GetRequiredService<IDocumentIngestionMessageHandler>();

        Assert.NotNull(pipeline);
        Assert.NotNull(handler);
    }

    private sealed class TestDocuMindDbContext(
        DbContextOptions<DocuMindDbContext> options,
        IOptions<PostgresOptions> postgresOptions) : DocuMindDbContext(options, postgresOptions)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<DocumentChunkEntity>();
        }
    }

    private sealed class FakeDocumentRepository : IDocumentRepository
    {
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Document?>(null);
        }

        public Task<bool> TryMarkProcessingIfUploadedAsync(
            Guid documentId,
            DateTimeOffset changedAtUtc,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Document document, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        public Task<StoredFile> SaveUploadAsync(
            Guid documentId,
            string fileName,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<StoredFile> MoveToProcessedAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeDocumentTextExtractor : IDocumentTextExtractor
    {
        public Task<TextExtractionResult> ExtractAsync(
            string fileName,
            string? contentType,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeDocumentChunker : IDocumentChunker
    {
        public IReadOnlyList<Chunk> Chunk(Guid documentId, string text)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeEmbeddingClient : IEmbeddingClient
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
