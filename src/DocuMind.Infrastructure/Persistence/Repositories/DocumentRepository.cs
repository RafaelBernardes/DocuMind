using DocuMind.Core.Documents;
using DocuMind.Core.Documents.IntegrationEvents;
using DocuMind.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using System.Text.Json;

namespace DocuMind.Infrastructure.Persistence.Repositories;

public sealed class DocumentRepository(DocuMindDbContext dbContext) : IDocumentRepository
{
    private const string DocumentUploadedMessageType = "documents.uploaded";

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Documents
            .AsNoTracking()
            .Include(document => document.Chunks)
            .SingleOrDefaultAsync(document => document.Id == id, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<bool> TryMarkProcessingIfUploadedAsync(
        Guid documentId,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedChangedAtUtc = changedAtUtc.ToUniversalTime();

        if (!dbContext.Database.IsRelational())
        {
            var document = await dbContext.Documents.SingleOrDefaultAsync(
                document => document.Id == documentId,
                cancellationToken);

            if (document is null || document.Status != DocumentStatus.Uploaded)
            {
                return false;
            }

            document.Status = DocumentStatus.Processing;
            document.UpdatedAtUtc = normalizedChangedAtUtc;
            document.FailureReason = null;
            document.RecoveryPoint = LastProcessingStage.Claimed;
            document.FailureCategory = null;
            document.ProcessingAttemptCount += 1;
            document.LastProcessingStartedAtUtc = normalizedChangedAtUtc;
            document.LastRecoveryPointAtUtc = normalizedChangedAtUtc;

            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        var affectedRows = await dbContext.Documents
            .Where(document => document.Id == documentId && document.Status == DocumentStatus.Uploaded)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(document => document.Status, DocumentStatus.Processing)
                    .SetProperty(document => document.UpdatedAtUtc, normalizedChangedAtUtc)
                    .SetProperty(document => document.FailureReason, (string?)null)
                    .SetProperty(document => document.RecoveryPoint, LastProcessingStage.Claimed)
                    .SetProperty(document => document.FailureCategory, (FailureCategory?)null)
                    .SetProperty(document => document.ProcessingAttemptCount, document => document.ProcessingAttemptCount + 1)
                    .SetProperty(document => document.LastProcessingStartedAtUtc, normalizedChangedAtUtc)
                    .SetProperty(document => document.LastRecoveryPointAtUtc, normalizedChangedAtUtc),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var entity = MapToEntity(document);
        var outboxMessage = MapUploadedDocumentToOutboxMessage(document);

        await dbContext.Documents.AddAsync(entity, cancellationToken);
        await dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var entity = await dbContext.Documents
            .Include(existingDocument => existingDocument.Chunks)
            .SingleAsync(existingDocument => existingDocument.Id == document.Id, cancellationToken);

        entity.FileName = document.Metadata.FileName;
        entity.ContentType = document.Metadata.ContentType;
        entity.SizeInBytes = document.Metadata.SizeInBytes;
        entity.Checksum = document.Metadata.Checksum;
        entity.StorageRelativePath = document.StorageRelativePath;
        entity.Status = document.Status;
        entity.UploadedAtUtc = document.UploadedAtUtc;
        entity.UpdatedAtUtc = document.UpdatedAtUtc;
        entity.FailureReason = document.FailureReason;
        entity.RecoveryPoint = document.LastProcessingStage;
        entity.FailureCategory = document.FailureCategory;
        entity.ProcessingAttemptCount = document.ProcessingAttemptCount;
        entity.LastProcessingStartedAtUtc = document.LastProcessingStartedAtUtc;
        entity.LastRecoveryPointAtUtc = document.LastProcessingStageAtUtc;

        if (entity.Chunks.Count > 0)
        {
            dbContext.DocumentChunks.RemoveRange(entity.Chunks);
            entity.Chunks.Clear();

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var chunk in document.Chunks)
        {
            entity.Chunks.Add(MapChunkToEntity(chunk));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    internal static DocumentEntity MapToEntity(Document document)
    {
        return new DocumentEntity
        {
            Id = document.Id,
            FileName = document.Metadata.FileName,
            ContentType = document.Metadata.ContentType,
            SizeInBytes = document.Metadata.SizeInBytes,
            Checksum = document.Metadata.Checksum,
            StorageRelativePath = document.StorageRelativePath,
            Status = document.Status,
            UploadedAtUtc = document.UploadedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            FailureReason = document.FailureReason,
            RecoveryPoint = document.LastProcessingStage,
            FailureCategory = document.FailureCategory,
            ProcessingAttemptCount = document.ProcessingAttemptCount,
            LastProcessingStartedAtUtc = document.LastProcessingStartedAtUtc,
            LastRecoveryPointAtUtc = document.LastProcessingStageAtUtc,
            Chunks = document.Chunks
                .Select(MapChunkToEntity)
                .ToList()
        };
    }

    private static Document MapToDomain(DocumentEntity entity)
    {
        var failureCategory = entity.FailureCategory;
        if (entity.Status == DocumentStatus.Failed && failureCategory is null)
        {
            failureCategory = FailureCategory.PermanentInvariant;
        }

        var metadata = new DocumentMetadata(
            entity.FileName,
            entity.ContentType,
            entity.SizeInBytes,
            entity.Checksum);

        var chunks = entity.Chunks
            .OrderBy(chunk => chunk.Order)
            .Select(chunk => new Chunk(
                chunk.Id,
                chunk.DocumentId,
                chunk.Order,
                chunk.Content,
                new ChunkMetadata(
                    chunk.CharacterCount,
                    chunk.TokenCount,
                    chunk.PageLabel),
                MapEmbeddingToDomain(chunk.Embedding)))
            .ToArray();

        return Document.Rehydrate(
            entity.Id,
            metadata,
            entity.StorageRelativePath,
            entity.Status,
            entity.UploadedAtUtc,
            entity.UpdatedAtUtc,
            entity.FailureReason,
            lastProcessingStage: entity.RecoveryPoint,
            failureCategory,
            entity.ProcessingAttemptCount,
            entity.LastProcessingStartedAtUtc,
            lastProcessingStageAtUtc: entity.LastRecoveryPointAtUtc,
            chunks);
    }

    private static DocumentChunkEntity MapChunkToEntity(Chunk chunk)
    {
        return new DocumentChunkEntity
        {
            Id = chunk.Id,
            DocumentId = chunk.DocumentId,
            Order = chunk.Order,
            Content = chunk.Content,
            CharacterCount = chunk.Metadata.CharacterCount,
            TokenCount = chunk.Metadata.TokenCount,
            PageLabel = chunk.Metadata.PageLabel,
            Embedding = MapEmbeddingToEntity(chunk.Embedding)
        };
    }

    private static Vector? MapEmbeddingToEntity(IReadOnlyList<float>? embedding)
    {
        return embedding is null ? null : new Vector(embedding.ToArray());
    }

    private static float[]? MapEmbeddingToDomain(Vector? embedding)
    {
        return embedding?.ToArray();
    }

    private static OutboxMessageEntity MapUploadedDocumentToOutboxMessage(Document document)
    {
        var message = new DocumentUploadedMessage(
            document.Id,
            document.Metadata.FileName,
            document.Metadata.ContentType,
            document.Metadata.SizeInBytes,
            document.StorageRelativePath,
            document.UploadedAtUtc);

        return new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = DocumentUploadedMessageType,
            DocumentId = document.Id,
            Payload = JsonSerializer.Serialize(message),
            OccurredAtUtc = document.UploadedAtUtc,
            ProcessedAtUtc = null,
            Error = null
        };
    }
}
