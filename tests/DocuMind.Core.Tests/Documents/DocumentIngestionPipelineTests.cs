using DocuMind.Core.Documents;
using DocuMind.Core.Storage;
using DocuMind.Infrastructure.Documents.Ingestion;
using Microsoft.Extensions.Logging;

namespace DocuMind.Core.Tests.Documents;

public sealed class DocumentIngestionPipelineTests
{
    [Fact]
    public async Task ProcessAsync_ShouldIndexUploadedDocument()
    {
        var document = CreateDocument();
        var repository = new FakeDocumentRepository(document);
        var fileStorage = new FakeFileStorage();
        var textExtractor = new FakeDocumentTextExtractor(
            TextExtractionResult.Success("first chunk second chunk"));
        var documentChunker = new FakeDocumentChunker();
        var embeddingClient = new FakeEmbeddingClient();
        var logger = new CapturingLogger<DocumentIngestionPipeline>();
        var pipeline = CreatePipeline(repository, fileStorage, textExtractor, documentChunker, embeddingClient, logger);

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Indexed, document.Status);
        Assert.Null(document.FailureReason);
        Assert.Equal(1, document.ProcessingAttemptCount);
        Assert.Equal(LastProcessingStage.IndexedPersisted, document.LastProcessingStage);
        Assert.Equal(
            [LastProcessingStage.FileOpened, LastProcessingStage.TextExtracted, LastProcessingStage.ChunksCreated, LastProcessingStage.EmbeddingsGenerated, LastProcessingStage.IndexedPersisted],
            repository.UpdatedDocuments.Select(updated => updated.LastProcessingStage).ToArray());
        Assert.Equal(1, repository.TryMarkProcessingCount);
        Assert.Equal("uploads/manual.pdf", fileStorage.CapturedRelativePath);
        Assert.Equal("manual.pdf", textExtractor.CapturedFileName);
        Assert.Equal("application/pdf", textExtractor.CapturedContentType);
        Assert.Equal(document.Id, documentChunker.CapturedDocumentId);
        Assert.Equal("first chunk second chunk", documentChunker.CapturedText);
        Assert.Equal(["first chunk", "second chunk"], embeddingClient.CapturedTexts);
        Assert.Equal(2, document.Chunks.Count);
        Assert.All(document.Chunks, chunk => Assert.NotNull(chunk.Embedding));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("indexed"));
    }

    [Fact]
    public async Task ProcessAsync_ShouldNoOpWhenClaimIsLostBeforeProcessing()
    {
        var document = CreateDocument();
        var repository = new FakeDocumentRepository(document)
        {
            TryMarkProcessingResult = false
        };
        var fileStorage = new FakeFileStorage();
        var textExtractor = new FakeDocumentTextExtractor(TextExtractionResult.Success("body"));
        var documentChunker = new FakeDocumentChunker();
        var embeddingClient = new FakeEmbeddingClient();
        var logger = new CapturingLogger<DocumentIngestionPipeline>();
        var pipeline = CreatePipeline(repository, fileStorage, textExtractor, documentChunker, embeddingClient, logger);

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        Assert.Null(document.FailureReason);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Equal(1, repository.TryMarkProcessingCount);
        Assert.Empty(repository.UpdatedStatuses);
        Assert.Null(fileStorage.CapturedRelativePath);
        Assert.Null(textExtractor.CapturedFileName);
        Assert.Null(documentChunker.CapturedText);
        Assert.Null(embeddingClient.CapturedTexts);
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("indexed"));
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("Failed"));
    }

    [Fact]
    public async Task ProcessAsync_ShouldIgnoreIndexedDocumentWithoutUpdatingOrDoingWork()
    {
        var document = RehydrateDocument(DocumentStatus.Indexed);
        var repository = new FakeDocumentRepository(document);
        var fileStorage = new FakeFileStorage();
        var textExtractor = new FakeDocumentTextExtractor(TextExtractionResult.Success("body"));
        var documentChunker = new FakeDocumentChunker();
        var embeddingClient = new FakeEmbeddingClient();
        var logger = new CapturingLogger<DocumentIngestionPipeline>();
        var pipeline = CreatePipeline(repository, fileStorage, textExtractor, documentChunker, embeddingClient, logger);

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Indexed, document.Status);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Null(fileStorage.CapturedRelativePath);
        Assert.Null(textExtractor.CapturedFileName);
        Assert.Null(documentChunker.CapturedText);
        Assert.Null(embeddingClient.CapturedTexts);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("persisted status is Indexed"));
    }

    [Fact]
    public async Task ProcessAsync_ShouldSkipProcessingDocumentWhenClaimIsStillActive()
    {
        var document = RehydrateDocument(
            DocumentStatus.Processing,
            lastProcessingStage: LastProcessingStage.TextExtracted,
            lastProcessingStartedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-2),
            lastProcessingStageAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1));
        var repository = new FakeDocumentRepository(document);
        var fileStorage = new FakeFileStorage();
        var textExtractor = new FakeDocumentTextExtractor(TextExtractionResult.Success("body"));
        var documentChunker = new FakeDocumentChunker();
        var embeddingClient = new FakeEmbeddingClient();
        var logger = new CapturingLogger<DocumentIngestionPipeline>();
        var pipeline = CreatePipeline(repository, fileStorage, textExtractor, documentChunker, embeddingClient, logger);

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Processing, document.Status);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Equal(0, repository.TryMarkProcessingCount);
        Assert.Null(fileStorage.CapturedRelativePath);
        Assert.Null(textExtractor.CapturedFileName);
        Assert.Null(documentChunker.CapturedText);
        Assert.Null(embeddingClient.CapturedTexts);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("claim is still active"));
    }

    [Fact]
    public async Task ProcessAsync_ShouldIgnoreFailedDocumentWithoutRetrying()
    {
        var document = RehydrateDocument(DocumentStatus.Failed, failureReason: "previous failure");
        var repository = new FakeDocumentRepository(document);
        var fileStorage = new FakeFileStorage();
        var textExtractor = new FakeDocumentTextExtractor(TextExtractionResult.Success("body"));
        var documentChunker = new FakeDocumentChunker();
        var embeddingClient = new FakeEmbeddingClient();
        var logger = new CapturingLogger<DocumentIngestionPipeline>();
        var pipeline = CreatePipeline(repository, fileStorage, textExtractor, documentChunker, embeddingClient, logger);

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("previous failure", document.FailureReason);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Null(fileStorage.CapturedRelativePath);
        Assert.Null(textExtractor.CapturedFileName);
        Assert.Null(documentChunker.CapturedText);
        Assert.Null(embeddingClient.CapturedTexts);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("persisted status is Failed"));
    }

    [Fact]
    public async Task ProcessAsync_ShouldMarkDocumentFailedWhenExtractionFails()
    {
        var document = CreateDocument();
        var repository = new FakeDocumentRepository(document);
        var pipeline = CreatePipeline(
            repository,
            textExtractor: new FakeDocumentTextExtractor(
                TextExtractionResult.Failure(
                    TextExtractionFailureCode.TextNotExtractable,
                    "No extractable text.")));

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("No extractable text.", document.FailureReason);
        Assert.Equal(FailureCategory.PermanentInput, document.FailureCategory);
        Assert.Empty(document.Chunks);
        Assert.Equal([LastProcessingStage.FileOpened, LastProcessingStage.FileOpened], repository.UpdatedDocuments.Select(updated => updated.LastProcessingStage).ToArray());
        Assert.Equal(1, repository.TryMarkProcessingCount);
    }

    [Fact]
    public async Task ProcessAsync_ShouldMarkDocumentFailedWhenEmbeddingGenerationThrows()
    {
        var document = CreateDocument();
        var repository = new FakeDocumentRepository(document);
        var pipeline = CreatePipeline(
            repository,
            embeddingClient: new FakeEmbeddingClient
            {
                Exception = new EmbeddingClientException("provider unavailable", isTransient: true)
            });

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("Embedding generation failed: provider unavailable", document.FailureReason);
        Assert.Equal(FailureCategory.RetryableDependency, document.FailureCategory);
        Assert.Empty(document.Chunks);
        Assert.Equal(
            [LastProcessingStage.FileOpened, LastProcessingStage.TextExtracted, LastProcessingStage.ChunksCreated, LastProcessingStage.ChunksCreated],
            repository.UpdatedDocuments.Select(updated => updated.LastProcessingStage).ToArray());
        Assert.Equal(1, repository.TryMarkProcessingCount);
    }

    [Fact]
    public async Task ProcessAsync_ShouldMarkDocumentFailedWhenEmbeddingCountDoesNotMatchChunks()
    {
        var document = CreateDocument();
        var repository = new FakeDocumentRepository(document);
        var pipeline = CreatePipeline(
            repository,
            embeddingClient: new FakeEmbeddingClient
            {
                Embeddings = [CreateEmbedding(1)]
            });

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("Document ingestion failed: Embedding client returned 1 embeddings for 2 chunks.", document.FailureReason);
        Assert.Equal(FailureCategory.PermanentInvariant, document.FailureCategory);
        Assert.Empty(document.Chunks);
        Assert.Equal(
            [LastProcessingStage.FileOpened, LastProcessingStage.TextExtracted, LastProcessingStage.ChunksCreated, LastProcessingStage.ChunksCreated],
            repository.UpdatedDocuments.Select(updated => updated.LastProcessingStage).ToArray());
        Assert.Equal(1, repository.TryMarkProcessingCount);
    }

    [Fact]
    public async Task ProcessAsync_ShouldMarkDocumentFailedWhenFinalIndexedUpdateFails()
    {
        var document = CreateDocument();
        var repository = new FakeDocumentRepository(document)
        {
            ThrowOnUpdateNumbers = [5]
        };
        var pipeline = CreatePipeline(repository);

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Failed, repository.UpdatedDocuments.Last().Status);
        Assert.Equal(FailureCategory.PersistenceFailure, repository.UpdatedDocuments.Last().FailureCategory);
        Assert.Equal("Document ingestion persistence failed: update failed", repository.UpdatedDocuments.Last().FailureReason);
        Assert.Equal(LastProcessingStage.EmbeddingsGenerated, repository.UpdatedDocuments.Last().LastProcessingStage);
        Assert.Empty(repository.UpdatedDocuments.Last().Chunks);
        Assert.Equal(1, repository.TryMarkProcessingCount);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRetryCancelledDocumentFromSourceAfterLastProcessingStageWasPersisted()
    {
        var document = CreateDocument();
        var repository = new FakeDocumentRepository(document);
        var cancellationPipeline = CreatePipeline(
            repository,
            textExtractor: new FakeDocumentTextExtractor(TextExtractionResult.Success("ignored"))
            {
                Exception = new OperationCanceledException(new CancellationToken(canceled: true))
            });

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            cancellationPipeline.ProcessAsync(CreateRequest(document.Id), CancellationToken.None));

        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        Assert.Equal(FailureCategory.Cancelled, document.FailureCategory);
        Assert.Equal(LastProcessingStage.FileOpened, document.LastProcessingStage);

        var retryPipeline = CreatePipeline(repository);

        await retryPipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Indexed, document.Status);
        Assert.Null(document.FailureReason);
        Assert.Null(document.FailureCategory);
        Assert.Equal(2, document.ProcessingAttemptCount);
        Assert.Equal(LastProcessingStage.IndexedPersisted, document.LastProcessingStage);
        Assert.Equal(
            [DocumentStatus.Processing, DocumentStatus.Processing, DocumentStatus.Uploaded],
            repository.UpdatedStatuses.Take(3).ToArray());
        Assert.Equal(DocumentStatus.Processing, repository.UpdatedStatuses[3]);
        Assert.Equal(DocumentStatus.Indexed, repository.UpdatedStatuses.Last());
    }

    [Fact]
    public async Task ProcessAsync_ShouldReclaimStaleProcessingDocumentAndRestartFromSource()
    {
        var staleStartedAtUtc = DateTimeOffset.UtcNow.AddHours(-1);
        var staleRecoveryAtUtc = DateTimeOffset.UtcNow.AddMinutes(-30);
        var document = RehydrateDocument(
            DocumentStatus.Processing,
            lastProcessingStage: LastProcessingStage.TextExtracted,
            lastProcessingStartedAtUtc: staleStartedAtUtc,
            lastProcessingStageAtUtc: staleRecoveryAtUtc);
        var repository = new FakeDocumentRepository(document);
        var fileStorage = new FakeFileStorage();
        var textExtractor = new FakeDocumentTextExtractor(TextExtractionResult.Success("body"));
        var logger = new CapturingLogger<DocumentIngestionPipeline>();
        var pipeline = CreatePipeline(
            repository,
            fileStorage: fileStorage,
            textExtractor: textExtractor,
            logger: logger);

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Indexed, document.Status);
        Assert.Equal(1, repository.TryMarkProcessingCount);
        Assert.Equal(DocumentStatus.Uploaded, repository.UpdatedDocuments.First().Status);
        Assert.Equal(LastProcessingStage.TextExtracted, repository.UpdatedDocuments.First().LastProcessingStage);
        Assert.Equal(DocumentStatus.Uploaded, repository.UpdatedStatuses.First());
        Assert.Equal(DocumentStatus.Processing, repository.UpdatedStatuses[1]);
        Assert.Equal(DocumentStatus.Indexed, repository.UpdatedStatuses.Last());
        Assert.Equal("uploads/manual.pdf", fileStorage.CapturedRelativePath);
        Assert.Equal("manual.pdf", textExtractor.CapturedFileName);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("Reclaiming stale processing claim"));
    }

    [Fact]
    public async Task ProcessAsync_ShouldPropagateCancellationWithoutMarkingDocumentFailed()
    {
        var document = CreateDocument();
        var repository = new FakeDocumentRepository(document);
        using var cancellationTokenSource = new CancellationTokenSource();
        var pipeline = CreatePipeline(
            repository,
            fileStorage: new FakeFileStorage
            {
                Exception = new OperationCanceledException(cancellationTokenSource.Token)
            });

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.ProcessAsync(CreateRequest(document.Id), cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        Assert.Null(document.FailureReason);
        Assert.Equal(FailureCategory.Cancelled, document.FailureCategory);
        Assert.Equal([DocumentStatus.Processing, DocumentStatus.Uploaded], repository.UpdatedStatuses);
        Assert.Null(repository.UpdatedDocuments.Last().FailureReason);
        Assert.Empty(repository.UpdatedDocuments.Last().Chunks);
        Assert.Equal(cancellationTokenSource.Token, repository.CapturedTryMarkCancellationTokens[0]);
        Assert.NotEqual(cancellationTokenSource.Token, repository.CapturedUpdateCancellationTokens.Last());
        Assert.False(repository.CapturedUpdateCancellationTokens.Last().IsCancellationRequested);
    }

    [Fact]
    public async Task ProcessAsync_ShouldPropagateCancellationWithoutCompensationWhenClaimIsCancelled()
    {
        var document = CreateDocument();
        using var cancellationTokenSource = new CancellationTokenSource();
        var repository = new FakeDocumentRepository(document)
        {
            ExceptionsByTryMarkNumber =
            {
                [1] = new OperationCanceledException(cancellationTokenSource.Token)
            }
        };
        var pipeline = CreatePipeline(repository);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.ProcessAsync(CreateRequest(document.Id), cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        Assert.Null(document.FailureReason);
        Assert.Empty(repository.UpdatedStatuses);
        Assert.Equal(cancellationTokenSource.Token, repository.CapturedTryMarkCancellationTokens[0]);
        Assert.Empty(repository.CapturedUpdateCancellationTokens);
    }

    [Fact]
    public async Task ProcessAsync_ShouldPropagateClaimFailureWithoutMarkingDocumentFailed()
    {
        var document = CreateDocument();
        var repository = new FakeDocumentRepository(document)
        {
            ExceptionsByTryMarkNumber =
            {
                [1] = new InvalidOperationException("claim failed")
            }
        };
        var fileStorage = new FakeFileStorage();
        var textExtractor = new FakeDocumentTextExtractor(TextExtractionResult.Success("body"));
        var documentChunker = new FakeDocumentChunker();
        var embeddingClient = new FakeEmbeddingClient();
        var pipeline = CreatePipeline(
            repository,
            fileStorage: fileStorage,
            textExtractor: textExtractor,
            documentChunker: documentChunker,
            embeddingClient: embeddingClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pipeline.ProcessAsync(CreateRequest(document.Id)));

        Assert.Equal("claim failed", exception.Message);
        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        Assert.Null(document.FailureReason);
        Assert.Empty(repository.UpdatedStatuses);
        Assert.Empty(repository.CapturedUpdateCancellationTokens);
        Assert.Null(fileStorage.CapturedRelativePath);
        Assert.Null(textExtractor.CapturedFileName);
        Assert.Null(documentChunker.CapturedText);
        Assert.Null(embeddingClient.CapturedTexts);
    }

    [Fact]
    public async Task ProcessAsync_ShouldSkipMissingDocument()
    {
        var repository = new FakeDocumentRepository(null);
        var logger = new CapturingLogger<DocumentIngestionPipeline>();
        var pipeline = CreatePipeline(repository, logger: logger);

        await pipeline.ProcessAsync(CreateRequest(Guid.NewGuid()));

        Assert.Equal(0, repository.UpdateCount);
        Assert.Equal(0, repository.TryMarkProcessingCount);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("was not found"));
    }

    [Theory]
    [InlineData(DocumentStatus.Indexed)]
    [InlineData(DocumentStatus.Failed)]
    public async Task ProcessAsync_ShouldSkipDocumentWithExplicitNonUploadedStatuses(DocumentStatus status)
    {
        var document = Document.Rehydrate(
            Guid.NewGuid(),
            new DocumentMetadata("manual.pdf", "application/pdf", 128),
            "uploads/manual.pdf",
            status,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            failureReason: status == DocumentStatus.Failed ? "failed before" : null,
            lastProcessingStage: LastProcessingStage.None,
            failureCategory: status == DocumentStatus.Failed ? FailureCategory.PermanentInvariant : null,
            chunks: []);
        var repository = new FakeDocumentRepository(document);
        var logger = new CapturingLogger<DocumentIngestionPipeline>();
        var pipeline = CreatePipeline(repository, logger: logger);

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(status, document.Status);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Equal(0, repository.TryMarkProcessingCount);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains($"persisted status is {status}"));
    }

    [Fact]
    public async Task ProcessAsync_ShouldSkipDocumentWithoutStorageRelativePath()
    {
        var document = Document.Rehydrate(
            Guid.NewGuid(),
            new DocumentMetadata("legacy.pdf", "application/pdf", 128),
            storageRelativePath: null,
            DocumentStatus.Uploaded,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            failureReason: null,
            lastProcessingStage: LastProcessingStage.None,
            failureCategory: null,
            chunks: []);
        var repository = new FakeDocumentRepository(document);
        var logger = new CapturingLogger<DocumentIngestionPipeline>();
        var pipeline = CreatePipeline(repository, logger: logger);

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Equal(0, repository.TryMarkProcessingCount);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("storage path"));
    }

    [Fact]
    public async Task ProcessAsync_ShouldNoOpWhenAnotherWorkerAlreadyClaimedProcessing()
    {
        var document = CreateDocument();
        var repository = new FakeDocumentRepository(document)
        {
            TryMarkProcessingResult = false
        };
        var fileStorage = new FakeFileStorage();
        var textExtractor = new FakeDocumentTextExtractor(TextExtractionResult.Success("body"));
        var documentChunker = new FakeDocumentChunker();
        var embeddingClient = new FakeEmbeddingClient();
        var logger = new CapturingLogger<DocumentIngestionPipeline>();
        var pipeline = CreatePipeline(
            repository,
            fileStorage: fileStorage,
            textExtractor: textExtractor,
            documentChunker: documentChunker,
            embeddingClient: embeddingClient,
            logger: logger);

        await pipeline.ProcessAsync(CreateRequest(document.Id));

        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        Assert.Equal(1, repository.TryMarkProcessingCount);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Null(fileStorage.CapturedRelativePath);
        Assert.Null(textExtractor.CapturedFileName);
        Assert.Null(documentChunker.CapturedText);
        Assert.Null(embeddingClient.CapturedTexts);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("already claimed"));
    }

    [Fact]
    public async Task ProcessAsync_ShouldPassCancellationTokenToDependencies()
    {
        var document = CreateDocument();
        var repository = new FakeDocumentRepository(document);
        var fileStorage = new FakeFileStorage();
        var textExtractor = new FakeDocumentTextExtractor(TextExtractionResult.Success("body"));
        var embeddingClient = new FakeEmbeddingClient();
        var pipeline = CreatePipeline(
            repository,
            fileStorage: fileStorage,
            textExtractor: textExtractor,
            embeddingClient: embeddingClient);
        using var cancellationTokenSource = new CancellationTokenSource();

        await pipeline.ProcessAsync(CreateRequest(document.Id), cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, repository.CapturedGetCancellationToken);
        Assert.Equal(cancellationTokenSource.Token, repository.CapturedTryMarkCancellationTokens[0]);
        Assert.Equal(cancellationTokenSource.Token, repository.CapturedUpdateCancellationTokens[0]);
        Assert.Equal(cancellationTokenSource.Token, fileStorage.CapturedCancellationToken);
        Assert.Equal(cancellationTokenSource.Token, textExtractor.CapturedCancellationToken);
        Assert.Equal(cancellationTokenSource.Token, embeddingClient.CapturedCancellationToken);
    }

    private static DocumentIngestionPipeline CreatePipeline(
        FakeDocumentRepository repository,
        FakeFileStorage? fileStorage = null,
        FakeDocumentTextExtractor? textExtractor = null,
        FakeDocumentChunker? documentChunker = null,
        FakeEmbeddingClient? embeddingClient = null,
        CapturingLogger<DocumentIngestionPipeline>? logger = null)
    {
        return new DocumentIngestionPipeline(
            repository,
            fileStorage ?? new FakeFileStorage(),
            textExtractor ?? new FakeDocumentTextExtractor(TextExtractionResult.Success("body")),
            documentChunker ?? new FakeDocumentChunker(),
            embeddingClient ?? new FakeEmbeddingClient(),
            new IngestionFailurePolicy(),
            logger ?? new CapturingLogger<DocumentIngestionPipeline>());
    }

    private static Document CreateDocument()
    {
        return new Document(
            Guid.NewGuid(),
            new DocumentMetadata("manual.pdf", "application/pdf", 128),
            "uploads/manual.pdf",
            new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero));
    }

    private static Document RehydrateDocument(
        DocumentStatus status,
        string? failureReason = null,
        LastProcessingStage lastProcessingStage = LastProcessingStage.None,
        DateTimeOffset? lastProcessingStartedAtUtc = null,
        DateTimeOffset? lastProcessingStageAtUtc = null)
    {
        return Document.Rehydrate(
            Guid.NewGuid(),
            new DocumentMetadata("manual.pdf", "application/pdf", 128),
            "uploads/manual.pdf",
            status,
            new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 13, 12, 5, 0, TimeSpan.Zero),
            failureReason,
            lastProcessingStage: lastProcessingStage,
            failureCategory: status == DocumentStatus.Failed ? FailureCategory.PermanentInvariant : null,
            processingAttemptCount: status == DocumentStatus.Uploaded ? 0 : 1,
            lastProcessingStartedAtUtc: lastProcessingStartedAtUtc,
            lastProcessingStageAtUtc: lastProcessingStageAtUtc,
            chunks: []);
    }

    private static DocumentIngestionRequest CreateRequest(Guid documentId)
    {
        return new DocumentIngestionRequest(
            documentId,
            "message-file-name.pdf",
            "text/plain",
            999,
            "uploads/from-message.pdf",
            DateTimeOffset.UtcNow);
    }

    private static float[] CreateEmbedding(float value)
    {
        return Enumerable
            .Range(0, EmbeddingConstants.ExpectedDimensions)
            .Select(_ => value)
            .ToArray();
    }

    private sealed class FakeDocumentRepository(Document? document) : IDocumentRepository
    {
        public List<Document> UpdatedDocuments { get; } = [];

        public List<DocumentStatus> UpdatedStatuses { get; } = [];

        public int[] ThrowOnUpdateNumbers { get; init; } = [];

        public Dictionary<int, Exception> ExceptionsByUpdateNumber { get; } = [];

        public Dictionary<int, Exception> ExceptionsByTryMarkNumber { get; } = [];

        public bool TryMarkProcessingResult { get; init; } = true;

        public int UpdateCount { get; private set; }

        public int TryMarkProcessingCount { get; private set; }

        public CancellationToken CapturedGetCancellationToken { get; private set; }

        public List<CancellationToken> CapturedUpdateCancellationTokens { get; } = [];

        public List<CancellationToken> CapturedTryMarkCancellationTokens { get; } = [];

        public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            CapturedGetCancellationToken = cancellationToken;

            return Task.FromResult(document is not null && document.Id == id ? document : null);
        }

        public Task<bool> TryMarkProcessingIfUploadedAsync(
            Guid documentId,
            DateTimeOffset changedAtUtc,
            CancellationToken cancellationToken = default)
        {
            TryMarkProcessingCount++;
            CapturedTryMarkCancellationTokens.Add(cancellationToken);

            if (ExceptionsByTryMarkNumber.TryGetValue(TryMarkProcessingCount, out var exception))
            {
                throw exception;
            }

            if (TryMarkProcessingResult)
            {
                UpdatedStatuses.Add(DocumentStatus.Processing);
            }

            return Task.FromResult(TryMarkProcessingResult);
        }

        public Task AddAsync(Document document, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            CapturedUpdateCancellationTokens.Add(cancellationToken);
            var snapshot = Document.Rehydrate(
                document.Id,
                document.Metadata,
                document.StorageRelativePath,
                document.Status,
                document.UploadedAtUtc,
                document.UpdatedAtUtc,
                document.FailureReason,
                document.LastProcessingStage,
                document.FailureCategory,
                document.ProcessingAttemptCount,
                document.LastProcessingStartedAtUtc,
                document.LastProcessingStageAtUtc,
                document.Chunks.ToArray());
            UpdatedDocuments.Add(snapshot);
            UpdatedStatuses.Add(snapshot.Status);

            if (ExceptionsByUpdateNumber.TryGetValue(UpdateCount, out var exception))
            {
                throw exception;
            }

            if (ThrowOnUpdateNumbers.Contains(UpdateCount))
            {
                throw new InvalidOperationException("update failed");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        public string? CapturedRelativePath { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public Exception? Exception { get; init; }

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
            CapturedRelativePath = relativePath;
            CapturedCancellationToken = cancellationToken;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
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
        private readonly TextExtractionResult _result;

        public FakeDocumentTextExtractor(TextExtractionResult result)
        {
            _result = result;
        }

        public string? CapturedFileName { get; private set; }

        public string? CapturedContentType { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public Exception? Exception { get; init; }

        public Task<TextExtractionResult> ExtractAsync(
            string fileName,
            string? contentType,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            CapturedFileName = fileName;
            CapturedContentType = contentType;
            CapturedCancellationToken = cancellationToken;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(_result);
        }
    }

    private sealed class FakeDocumentChunker : IDocumentChunker
    {
        public Guid CapturedDocumentId { get; private set; }

        public string? CapturedText { get; private set; }

        public IReadOnlyList<Chunk> Chunk(Guid documentId, string text)
        {
            CapturedDocumentId = documentId;
            CapturedText = text;

            return
            [
                new Chunk(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    documentId,
                    0,
                    "first chunk",
                    new ChunkMetadata(characterCount: 11)),
                new Chunk(
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    documentId,
                    1,
                    "second chunk",
                    new ChunkMetadata(characterCount: 12))
            ];
        }
    }

    private sealed class FakeEmbeddingClient : IEmbeddingClient
    {
        public IReadOnlyList<string>? CapturedTexts { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public Exception? Exception { get; init; }

        public IReadOnlyList<float[]>? Embeddings { get; init; }

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            CapturedTexts = texts;
            CapturedCancellationToken = cancellationToken;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult<IReadOnlyList<float[]>>(
                Embeddings ?? texts.Select((_, index) => CreateEmbedding(index + 1)).ToArray());
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
