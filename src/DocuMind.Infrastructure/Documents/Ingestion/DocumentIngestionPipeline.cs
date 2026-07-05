using DocuMind.Core.Documents;
using DocuMind.Core.Storage;
using Microsoft.Extensions.Logging;

namespace DocuMind.Infrastructure.Documents.Ingestion;

public sealed class DocumentIngestionPipeline(
    IDocumentRepository documentRepository,
    IFileStorage fileStorage,
    IDocumentTextExtractor textExtractor,
    IDocumentChunker documentChunker,
    IEmbeddingClient embeddingClient,
    IngestionFailurePolicy failurePolicy,
    ILogger<DocumentIngestionPipeline> logger) : IDocumentIngestionPipeline
{
    private static readonly TimeSpan CompensationTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessingClaimTimeout = TimeSpan.FromMinutes(15);

    public async Task ProcessAsync(DocumentIngestionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var processingClaimed = false;
        var finalPersistenceAttempted = false;
        var document = await documentRepository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document is null)
        {
            logger.LogWarning(
                "Skipping document ingestion for DocumentId {DocumentId} because the persisted document was not found.",
                request.DocumentId);
            return;
        }

        if (document.Status is DocumentStatus.Indexed or DocumentStatus.Failed)
        {
            logger.LogInformation(
                "Skipping document ingestion for DocumentId {DocumentId} because persisted status is {Status}.",
                document.Id,
                document.Status);
            return;
        }

        if (document.Status == DocumentStatus.Processing)
        {
            if (!IsStaleProcessing(document))
            {
                logger.LogInformation(
                    "Skipping document ingestion for DocumentId {DocumentId} because persisted status is Processing and the claim is still active.",
                    document.Id);
                return;
            }

            logger.LogWarning(
                "Reclaiming stale processing claim for DocumentId {DocumentId}; LastProcessingStage {LastProcessingStage} is recorded for diagnostics.",
                document.Id,
                document.LastProcessingStage);

            document.RequeueForRetry();
            await documentRepository.UpdateAsync(document, cancellationToken);
        }

        if (document.Status != DocumentStatus.Uploaded)
        {
            logger.LogWarning(
                "Skipping document ingestion for DocumentId {DocumentId} because persisted status {Status} is unsupported for upload processing.",
                document.Id,
                document.Status);
            return;
        }

        if (string.IsNullOrWhiteSpace(document.StorageRelativePath))
        {
            logger.LogWarning(
                "Skipping document ingestion for DocumentId {DocumentId} because the persisted storage path is empty.",
                document.Id);
            return;
        }

        try
        {
            var changedAtUtc = DateTimeOffset.UtcNow;
            processingClaimed = await documentRepository.TryMarkProcessingIfUploadedAsync(
                document.Id,
                changedAtUtc,
                cancellationToken);

            if (!processingClaimed)
            {
                logger.LogInformation(
                    "Skipping document ingestion for DocumentId {DocumentId} because another worker already claimed processing.",
                    document.Id);
                return;
            }

            document.MarkProcessing(changedAtUtc);

            logger.LogInformation(
                "Document ingestion marked persisted document as Processing for DocumentId {DocumentId}.",
                document.Id);

            await using var content = await fileStorage.OpenReadAsync(document.StorageRelativePath, cancellationToken);
            await RecordProcessingStageAsync(document, LastProcessingStage.FileOpened, cancellationToken);

            var extractionResult = await textExtractor.ExtractAsync(
                document.Metadata.FileName,
                document.Metadata.ContentType,
                content,
                cancellationToken);

            if (!extractionResult.IsSuccess)
            {
                await MarkFailedAsync(
                    document,
                    failurePolicy.ClassifyExtractionFailure(extractionResult),
                    cancellationToken);
                return;
            }

            await RecordProcessingStageAsync(document, LastProcessingStage.TextExtracted, cancellationToken);

            var chunks = documentChunker.Chunk(document.Id, extractionResult.Text!);
            await RecordProcessingStageAsync(document, LastProcessingStage.ChunksCreated, cancellationToken);

            var embeddings = await embeddingClient.GenerateEmbeddingsAsync(
                chunks.Select(chunk => chunk.Content).ToArray(),
                cancellationToken);

            if (embeddings.Count != chunks.Count)
            {
                throw new InvalidOperationException(
                    $"Embedding client returned {embeddings.Count} embeddings for {chunks.Count} chunks.");
            }

            await RecordProcessingStageAsync(document, LastProcessingStage.EmbeddingsGenerated, cancellationToken);

            var indexedChunks = chunks
                .Select((chunk, index) => new Chunk(
                    chunk.Id,
                    chunk.DocumentId,
                    chunk.Order,
                    chunk.Content,
                    chunk.Metadata,
                    embeddings[index]))
                .ToArray();

            document.MarkIndexed(indexedChunks);
            finalPersistenceAttempted = true;
            await documentRepository.UpdateAsync(document, cancellationToken);

            logger.LogInformation(
                "Document ingestion indexed DocumentId {DocumentId} with {ChunkCount} chunks.",
                document.Id,
                indexedChunks.Length);
        }
        catch (OperationCanceledException exception)
        {
            if (processingClaimed)
            {
                await TryCompensateToUploadedAsync(document, exception);
            }

            throw;
        }
        catch (Exception exception)
        {
            if (!processingClaimed)
            {
                logger.LogError(
                    exception,
                    "Document ingestion failed before claiming processing for DocumentId {DocumentId}.",
                    document.Id);
                throw;
            }

            logger.LogError(
                exception,
                "Document ingestion failed while processing DocumentId {DocumentId}.",
                document.Id);

            var failure = finalPersistenceAttempted
                ? failurePolicy.PersistenceFailure(exception)
                : failurePolicy.ClassifyException(exception);
            await MarkFailedAsync(
                document,
                failure,
                cancellationToken,
                finalPersistenceAttempted ? LastProcessingStage.EmbeddingsGenerated : null);
        }
    }

    private async Task RecordProcessingStageAsync(
        Document document,
        LastProcessingStage lastProcessingStage,
        CancellationToken cancellationToken)
    {
        document.RecordProcessingStage(lastProcessingStage);
        await documentRepository.UpdateAsync(document, cancellationToken);
    }

    private async Task TryCompensateToUploadedAsync(Document document, OperationCanceledException exception)
    {
        document.CompensateCancellation();

        try
        {
            using var compensationCancellationTokenSource = new CancellationTokenSource(CompensationTimeout);
            await documentRepository.UpdateAsync(document, compensationCancellationTokenSource.Token);

            logger.LogWarning(
                exception,
                "Document ingestion cancellation compensated DocumentId {DocumentId} back to Uploaded.",
                document.Id);
        }
        catch (Exception compensationException)
        {
            logger.LogWarning(
                compensationException,
                "Document ingestion cancellation could not compensate DocumentId {DocumentId} back to Uploaded.",
                document.Id);
        }
    }

    private static bool IsStaleProcessing(Document document)
    {
        var referenceTimestamp = document.LastProcessingStageAtUtc ?? document.LastProcessingStartedAtUtc;
        if (referenceTimestamp is null)
        {
            return true;
        }

        return DateTimeOffset.UtcNow - referenceTimestamp.Value >= ProcessingClaimTimeout;
    }

    private async Task MarkFailedAsync(
        Document document,
        IngestionFailure failure,
        CancellationToken cancellationToken,
        LastProcessingStage? lastProcessingStageOverride = null)
    {
        var failedDocument = document.Status == DocumentStatus.Processing && lastProcessingStageOverride is null
            ? document
            : Document.Rehydrate(
                document.Id,
                document.Metadata,
                document.StorageRelativePath,
                DocumentStatus.Processing,
                document.UploadedAtUtc,
                document.UpdatedAtUtc,
                failureReason: null,
                lastProcessingStage: lastProcessingStageOverride ?? document.LastProcessingStage,
                failureCategory: null,
                processingAttemptCount: document.ProcessingAttemptCount,
                lastProcessingStartedAtUtc: document.LastProcessingStartedAtUtc,
                lastProcessingStageAtUtc: document.LastProcessingStageAtUtc,
                chunks: []);

        failedDocument.MarkFailed(failure.Category, failure.Reason);
        await documentRepository.UpdateAsync(failedDocument, cancellationToken);

        logger.LogWarning(
            "Document ingestion marked DocumentId {DocumentId} as Failed.",
            failedDocument.Id);
    }
}
