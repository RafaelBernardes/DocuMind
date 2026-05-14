using DocuMind.Core.Documents;
using DocuMind.Core.Documents.IntegrationEvents;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Persistence.Entities;
using DocuMind.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DocuMind.Core.Tests.Persistence;

public sealed class DocumentRepositoryOutboxTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistDocumentAndOutboxMessage()
    {
        await using var dbContext = CreateDbContext();
        var repository = new DocumentRepository(dbContext);
        var uploadedAtUtc = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var document = new Document(
            Guid.NewGuid(),
            new DocumentMetadata("manual.pdf", "application/pdf", 128),
            "uploads/manual.pdf",
            uploadedAtUtc);

        await repository.AddAsync(document);

        var persistedDocument = await dbContext.Documents.SingleAsync();
        var persistedOutboxMessage = await dbContext.OutboxMessages.SingleAsync();
        var payload = JsonSerializer.Deserialize<DocumentUploadedMessage>(persistedOutboxMessage.Payload);

        Assert.Equal(document.Id, persistedDocument.Id);
        Assert.Equal(DocumentStatus.Uploaded, persistedDocument.Status);
        Assert.Equal("documents.uploaded", persistedOutboxMessage.Type);
        Assert.Equal(document.Id, persistedOutboxMessage.DocumentId);
        Assert.Equal(uploadedAtUtc, persistedOutboxMessage.OccurredAtUtc);
        Assert.Null(persistedOutboxMessage.ProcessedAtUtc);
        Assert.NotNull(payload);
        Assert.Equal(document.Id, payload!.DocumentId);
        Assert.Equal(document.Metadata.FileName, payload.FileName);
        Assert.Equal(document.StorageRelativePath, payload.StorageRelativePath);
    }

    [Fact]
    public void Model_ShouldExposeOutboxMessagesSet()
    {
        using var dbContext = CreateDbContext();

        var entityType = dbContext.Model.FindEntityType("DocuMind.Infrastructure.Persistence.Entities.OutboxMessageEntity");

        Assert.NotNull(entityType);
        Assert.Equal("outbox_messages", entityType!.GetTableName());
        Assert.NotNull(entityType.FindProperty("DocumentId"));
    }

    private static DocuMindDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DocuMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new TestDocuMindDbContext(
            options,
            Options.Create(new PostgresOptions
            {
                ConnectionString = "Host=localhost;Database=documind;Username=postgres;Password=postgres",
                Schema = "public"
            }));
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
}
