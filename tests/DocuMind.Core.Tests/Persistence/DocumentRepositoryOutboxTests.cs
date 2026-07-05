using DocuMind.Core.Documents;
using DocuMind.Core.Documents.IntegrationEvents;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Persistence.Entities;
using DocuMind.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
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
    public async Task AddAsync_AndGetByIdAsync_ShouldRoundTripChunkEmbedding()
    {
        await using var dbContext = CreateDbContext();
        var repository = new DocumentRepository(dbContext);
        var uploadedAtUtc = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var documentId = Guid.NewGuid();
        var embedding = Enumerable
            .Range(0, EmbeddingConstants.ExpectedDimensions)
            .Select(index => index == 0 ? 0.25f : (float)index)
            .ToArray();
        var chunk = new Chunk(
            Guid.NewGuid(),
            documentId,
            0,
            "chunk body",
            new ChunkMetadata(characterCount: 10, tokenCount: 3, pageLabel: "1"),
            embedding);
        var document = Document.Rehydrate(
            documentId,
            new DocumentMetadata("manual.pdf", "application/pdf", 128),
            "uploads/manual.pdf",
            DocumentStatus.Indexed,
            uploadedAtUtc,
            uploadedAtUtc.AddMinutes(5),
            null,
            LastProcessingStage.None,
            null,
            0,
            null,
            null,
            [chunk]);

        await repository.AddAsync(document);
        dbContext.ChangeTracker.Clear();

        var rehydrated = await repository.GetByIdAsync(documentId);

        Assert.NotNull(rehydrated);
        Assert.Single(rehydrated!.Chunks);
        Assert.NotNull(rehydrated.Chunks.First().Embedding);
        Assert.Equal(embedding, rehydrated.Chunks.First().Embedding);
    }

    [Fact]
    public async Task AddAsync_AndGetByIdAsync_ShouldPreserveNullChunkEmbedding()
    {
        await using var dbContext = CreateDbContext();
        var repository = new DocumentRepository(dbContext);
        var uploadedAtUtc = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var documentId = Guid.NewGuid();
        var chunk = new Chunk(
            Guid.NewGuid(),
            documentId,
            0,
            "chunk body",
            new ChunkMetadata(characterCount: 10, tokenCount: 3, pageLabel: "1"));
        var document = Document.Rehydrate(
            documentId,
            new DocumentMetadata("manual.pdf", "application/pdf", 128),
            "uploads/manual.pdf",
            DocumentStatus.Indexed,
            uploadedAtUtc,
            uploadedAtUtc.AddMinutes(5),
            null,
            LastProcessingStage.None,
            null,
            0,
            null,
            null,
            [chunk]);

        await repository.AddAsync(document);
        dbContext.ChangeTracker.Clear();

        var rehydrated = await repository.GetByIdAsync(documentId);

        Assert.NotNull(rehydrated);
        Assert.Single(rehydrated!.Chunks);
        Assert.Null(rehydrated.Chunks.First().Embedding);
    }

    [Fact]
    public async Task TryMarkProcessingIfUploadedAsync_ShouldClaimUploadedDocument()
    {
        await using var dbContext = CreateDbContext();
        var repository = new DocumentRepository(dbContext);
        var uploadedAtUtc = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var changedAtUtc = new DateTimeOffset(2026, 5, 13, 12, 10, 0, TimeSpan.FromHours(-3));
        var document = new Document(
            Guid.NewGuid(),
            new DocumentMetadata("manual.pdf", "application/pdf", 128),
            "uploads/manual.pdf",
            uploadedAtUtc);

        await repository.AddAsync(document);
        dbContext.ChangeTracker.Clear();

        var claimed = await repository.TryMarkProcessingIfUploadedAsync(document.Id, changedAtUtc);

        dbContext.ChangeTracker.Clear();
        var persistedDocument = await dbContext.Documents.SingleAsync(document => document.Id == document.Id);
        Assert.True(claimed);
        Assert.Equal(DocumentStatus.Processing, persistedDocument.Status);
        Assert.Equal(changedAtUtc.ToUniversalTime(), persistedDocument.UpdatedAtUtc);
        Assert.Null(persistedDocument.FailureReason);
        Assert.Equal(LastProcessingStage.Claimed, persistedDocument.RecoveryPoint);
        Assert.Null(persistedDocument.FailureCategory);
        Assert.Equal(1, persistedDocument.ProcessingAttemptCount);
        Assert.Equal(changedAtUtc.ToUniversalTime(), persistedDocument.LastProcessingStartedAtUtc);
        Assert.Equal(changedAtUtc.ToUniversalTime(), persistedDocument.LastRecoveryPointAtUtc);
    }

    [Theory]
    [InlineData(DocumentStatus.Processing)]
    [InlineData(DocumentStatus.Indexed)]
    [InlineData(DocumentStatus.Failed)]
    public async Task TryMarkProcessingIfUploadedAsync_ShouldNotClaimNonUploadedDocument(DocumentStatus status)
    {
        await using var dbContext = CreateDbContext();
        var repository = new DocumentRepository(dbContext);
        var uploadedAtUtc = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var updatedAtUtc = uploadedAtUtc.AddMinutes(5);
        var documentId = Guid.NewGuid();
        var document = Document.Rehydrate(
            documentId,
            new DocumentMetadata("manual.pdf", "application/pdf", 128),
            "uploads/manual.pdf",
            status,
            uploadedAtUtc,
            updatedAtUtc,
            status == DocumentStatus.Failed ? "previous failure" : null,
            LastProcessingStage.None,
            status == DocumentStatus.Failed ? FailureCategory.PermanentInvariant : null,
            0,
            null,
            null,
            status == DocumentStatus.Indexed ? [CreateChunk(documentId)] : []);

        await repository.AddAsync(document);
        dbContext.ChangeTracker.Clear();

        var claimed = await repository.TryMarkProcessingIfUploadedAsync(
            document.Id,
            updatedAtUtc.AddMinutes(1));

        dbContext.ChangeTracker.Clear();
        var persistedDocument = await dbContext.Documents.SingleAsync(entity => entity.Id == document.Id);
        Assert.False(claimed);
        Assert.Equal(status, persistedDocument.Status);
        Assert.Equal(updatedAtUtc, persistedDocument.UpdatedAtUtc);
    }

    [Fact]
    public async Task AddAsync_AndGetByIdAsync_ShouldRoundTripProcessingDiagnostics()
    {
        await using var dbContext = CreateDbContext();
        var repository = new DocumentRepository(dbContext);
        var uploadedAtUtc = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var recoveryAtUtc = uploadedAtUtc.AddMinutes(3);
        var documentId = Guid.NewGuid();
        var document = Document.Rehydrate(
            documentId,
            new DocumentMetadata("manual.pdf", "application/pdf", 128),
            "uploads/manual.pdf",
            DocumentStatus.Failed,
            uploadedAtUtc,
            recoveryAtUtc,
            "previous failure",
            LastProcessingStage.TextExtracted,
            FailureCategory.PersistenceFailure,
            2,
            uploadedAtUtc.AddMinutes(1),
            recoveryAtUtc,
            []);

        await repository.AddAsync(document);
        dbContext.ChangeTracker.Clear();

        var rehydrated = await repository.GetByIdAsync(documentId);

        Assert.NotNull(rehydrated);
        Assert.Equal(LastProcessingStage.TextExtracted, rehydrated!.LastProcessingStage);
        Assert.Equal(FailureCategory.PersistenceFailure, rehydrated.FailureCategory);
        Assert.Equal(2, rehydrated.ProcessingAttemptCount);
        Assert.Equal(uploadedAtUtc.AddMinutes(1), rehydrated.LastProcessingStartedAtUtc);
        Assert.Equal(recoveryAtUtc, rehydrated.LastProcessingStageAtUtc);
        Assert.Equal("previous failure", rehydrated.FailureReason);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldRehydrateLegacyFailedDocumentWithoutFailureCategory()
    {
        await using var dbContext = CreateDbContext();
        var repository = new DocumentRepository(dbContext);
        var uploadedAtUtc = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var updatedAtUtc = uploadedAtUtc.AddMinutes(4);
        var documentId = Guid.NewGuid();

        dbContext.Documents.Add(new DocumentEntity
        {
            Id = documentId,
            FileName = "manual.pdf",
            ContentType = "application/pdf",
            SizeInBytes = 128,
            Checksum = null,
            StorageRelativePath = "uploads/manual.pdf",
            Status = DocumentStatus.Failed,
            UploadedAtUtc = uploadedAtUtc,
            UpdatedAtUtc = updatedAtUtc,
            FailureReason = "legacy failure",
            RecoveryPoint = LastProcessingStage.None,
            FailureCategory = null,
            ProcessingAttemptCount = 0,
            LastProcessingStartedAtUtc = null,
            LastRecoveryPointAtUtc = null,
            Chunks = []
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var rehydrated = await repository.GetByIdAsync(documentId);

        Assert.NotNull(rehydrated);
        Assert.Equal(DocumentStatus.Failed, rehydrated!.Status);
        Assert.Equal(FailureCategory.PermanentInvariant, rehydrated.FailureCategory);
        Assert.Equal("legacy failure", rehydrated.FailureReason);
    }

    [Fact]
    public async Task TryMarkProcessingIfUploadedAsync_ShouldReturnFalseWhenDocumentDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var repository = new DocumentRepository(dbContext);

        var claimed = await repository.TryMarkProcessingIfUploadedAsync(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.False(claimed);
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

    private static Chunk CreateChunk(Guid documentId)
    {
        return new Chunk(
            Guid.NewGuid(),
            documentId,
            0,
            "chunk body",
            new ChunkMetadata(characterCount: 10));
    }

    private sealed class TestDocuMindDbContext(
        DbContextOptions<DocuMindDbContext> options,
        IOptions<PostgresOptions> postgresOptions) : DocuMindDbContext(options, postgresOptions)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DocumentChunkEntity>()
                .Property(chunk => chunk.Embedding)
                .HasConversion(
                    embedding => SerializeEmbedding(embedding),
                    value => DeserializeEmbedding(value));
        }
    }

    private static string? SerializeEmbedding(Vector? embedding)
    {
        return embedding is null ? null : JsonSerializer.Serialize(embedding.ToArray());
    }

    private static Vector? DeserializeEmbedding(string? value)
    {
        return value is null ? null : new Vector(JsonSerializer.Deserialize<float[]>(value)!);
    }
}
