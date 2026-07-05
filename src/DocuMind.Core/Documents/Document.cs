namespace DocuMind.Core.Documents;

using FailureCategoryType = global::DocuMind.Core.Documents.FailureCategory;

public sealed class Document
{
    private readonly List<Chunk> _chunks = [];

    public Document(
        Guid id,
        DocumentMetadata metadata,
        string storageRelativePath,
        DateTimeOffset? uploadedAtUtc = null)
        : this(
            id,
            metadata,
            storageRelativePath,
            requireStorageRelativePath: true,
            DocumentStatus.Uploaded,
            uploadedAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
            uploadedAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
            failureReason: null,
            lastProcessingStage: LastProcessingStage.None,
            failureCategory: null,
            processingAttemptCount: 0,
            lastProcessingStartedAtUtc: null,
            lastProcessingStageAtUtc: null,
            chunks: null)
    {
    }

    private Document(
        Guid id,
        DocumentMetadata metadata,
        string? storageRelativePath,
        bool requireStorageRelativePath,
        DocumentStatus status,
        DateTimeOffset uploadedAtUtc,
        DateTimeOffset updatedAtUtc,
        string? failureReason,
        LastProcessingStage lastProcessingStage,
        FailureCategoryType? failureCategory,
        int processingAttemptCount,
        DateTimeOffset? lastProcessingStartedAtUtc,
        DateTimeOffset? lastProcessingStageAtUtc,
        IEnumerable<Chunk>? chunks)
    {
        Id = id != Guid.Empty ? id : throw new ArgumentException("Document id is required.", nameof(id));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        StorageRelativePath = requireStorageRelativePath
            ? RequireStorageRelativePath(storageRelativePath!)
            : NormalizeStorageRelativePath(storageRelativePath);
        Status = status;
        UploadedAtUtc = uploadedAtUtc.ToUniversalTime();
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
        LastProcessingStage = lastProcessingStage;
        FailureCategory = failureCategory;
        ProcessingAttemptCount = processingAttemptCount >= 0
            ? processingAttemptCount
            : throw new ArgumentOutOfRangeException(nameof(processingAttemptCount));
        LastProcessingStartedAtUtc = lastProcessingStartedAtUtc?.ToUniversalTime();
        LastProcessingStageAtUtc = lastProcessingStageAtUtc?.ToUniversalTime();

        ValidateState();

        if (chunks is null)
        {
            return;
        }

        var materializedChunks = chunks.ToList();
        if (materializedChunks.Any(chunk => chunk is null))
        {
            throw new ArgumentException("Chunks cannot contain null items during rehydration.", nameof(chunks));
        }

        if (materializedChunks.Any(chunk => chunk.DocumentId != id))
        {
            throw new ArgumentException("All chunks must belong to the document being rehydrated.", nameof(chunks));
        }

        _chunks.AddRange(materializedChunks);
    }

    public static Document Rehydrate(
        Guid id,
        DocumentMetadata metadata,
        string? storageRelativePath,
        DocumentStatus status,
        DateTimeOffset uploadedAtUtc,
        DateTimeOffset updatedAtUtc,
        string? failureReason,
        IEnumerable<Chunk>? chunks)
    {
        return Rehydrate(
            id,
            metadata,
            storageRelativePath,
            status,
            uploadedAtUtc,
            updatedAtUtc,
            failureReason,
            LastProcessingStage.None,
            failureCategory: null,
            processingAttemptCount: 0,
            lastProcessingStartedAtUtc: null,
            lastProcessingStageAtUtc: null,
            chunks);
    }

    public static Document Rehydrate(
        Guid id,
        DocumentMetadata metadata,
        string? storageRelativePath,
        DocumentStatus status,
        DateTimeOffset uploadedAtUtc,
        DateTimeOffset updatedAtUtc,
        string? failureReason,
        LastProcessingStage lastProcessingStage = LastProcessingStage.None,
        FailureCategoryType? failureCategory = null,
        int processingAttemptCount = 0,
        DateTimeOffset? lastProcessingStartedAtUtc = null,
        DateTimeOffset? lastProcessingStageAtUtc = null,
        IEnumerable<Chunk>? chunks = null)
    {
        return NewRehydrated(
            id,
            metadata,
            storageRelativePath,
            status,
            uploadedAtUtc,
            updatedAtUtc,
            failureReason,
            lastProcessingStage,
            failureCategory,
            processingAttemptCount,
            lastProcessingStartedAtUtc,
            lastProcessingStageAtUtc,
            chunks);
    }

    private static Document NewRehydrated(
        Guid id,
        DocumentMetadata metadata,
        string? storageRelativePath,
        DocumentStatus status,
        DateTimeOffset uploadedAtUtc,
        DateTimeOffset updatedAtUtc,
        string? failureReason,
        LastProcessingStage lastProcessingStage,
        FailureCategoryType? failureCategory,
        int processingAttemptCount,
        DateTimeOffset? lastProcessingStartedAtUtc,
        DateTimeOffset? lastProcessingStageAtUtc,
        IEnumerable<Chunk>? chunks)
    {
        return new Document(
            id,
            metadata,
            storageRelativePath,
            requireStorageRelativePath: false,
            status,
            uploadedAtUtc,
            updatedAtUtc,
            failureReason,
            lastProcessingStage,
            failureCategory,
            processingAttemptCount,
            lastProcessingStartedAtUtc,
            lastProcessingStageAtUtc,
            chunks);
    }

    public Guid Id { get; }

    public DocumentMetadata Metadata { get; }

    public string? StorageRelativePath { get; }

    public DocumentStatus Status { get; private set; }

    public DateTimeOffset UploadedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string? FailureReason { get; private set; }

    public LastProcessingStage LastProcessingStage { get; private set; }

    public FailureCategoryType? FailureCategory { get; private set; }

    public int ProcessingAttemptCount { get; private set; }

    public DateTimeOffset? LastProcessingStartedAtUtc { get; private set; }

    public DateTimeOffset? LastProcessingStageAtUtc { get; private set; }

    public IReadOnlyCollection<Chunk> Chunks => _chunks.AsReadOnly();

    public void MarkProcessing(DateTimeOffset? changedAtUtc = null)
    {
        var changedAt = NormalizeChangedAtUtc(changedAtUtc);

        TransitionTo(DocumentStatus.Processing, changedAt);
        ProcessingAttemptCount++;
        LastProcessingStartedAtUtc = changedAt;
        FailureCategory = null;
        FailureReason = null;

        if (LastProcessingStage > LastProcessingStage.Claimed)
        {
            LastProcessingStage = LastProcessingStage.Claimed;
        }

        AdvanceProcessingStage(LastProcessingStage.Claimed, changedAt, updateTimestamp: false);
    }

    public void RecordProcessingStage(LastProcessingStage lastProcessingStage, DateTimeOffset? changedAtUtc = null)
    {
        if (lastProcessingStage == LastProcessingStage.None)
        {
            throw new ArgumentException("Processing stage must be a concrete processing stage.", nameof(lastProcessingStage));
        }

        if (Status != DocumentStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Cannot record processing stage {lastProcessingStage} while document is {Status}.");
        }

        var changedAt = NormalizeChangedAtUtc(changedAtUtc);
        AdvanceProcessingStage(lastProcessingStage, changedAt, updateTimestamp: true);
    }

    public void MarkIndexed(IEnumerable<Chunk> chunks, DateTimeOffset? changedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var materializedChunks = chunks.ToList();
        if (materializedChunks.Count == 0)
        {
            throw new ArgumentException("At least one chunk is required to index a document.", nameof(chunks));
        }

        if (materializedChunks.Any(chunk => chunk is null))
        {
            throw new ArgumentException("Chunks cannot contain null items.", nameof(chunks));
        }

        if (materializedChunks.Any(chunk => chunk.DocumentId != Id))
        {
            throw new ArgumentException("All chunks must belong to the document being indexed.", nameof(chunks));
        }

        var changedAt = NormalizeChangedAtUtc(changedAtUtc);

        TransitionTo(DocumentStatus.Indexed, changedAt);

        _chunks.Clear();
        _chunks.AddRange(materializedChunks);
        FailureReason = null;
        FailureCategory = null;
        AdvanceProcessingStage(LastProcessingStage.IndexedPersisted, changedAt, updateTimestamp: false);
    }

    public void MarkFailed(string reason, DateTimeOffset? changedAtUtc = null)
    {
        MarkFailed(FailureCategoryType.PermanentInvariant, reason, changedAtUtc);
    }

    public void MarkFailed(
        FailureCategory failureCategory,
        string reason,
        DateTimeOffset? changedAtUtc = null)
    {
        if (failureCategory == FailureCategoryType.Cancelled)
        {
            throw new ArgumentException("Cancellation must not mark the document as failed.", nameof(failureCategory));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(reason));
        }

        TransitionTo(DocumentStatus.Failed, NormalizeChangedAtUtc(changedAtUtc));
        FailureReason = reason.Trim();
        FailureCategory = failureCategory;
    }

    public void CompensateCancellation(DateTimeOffset? changedAtUtc = null)
    {
        RequeueForRetry(changedAtUtc);
        FailureCategory = FailureCategoryType.Cancelled;
    }

    public void RequeueForRetry(DateTimeOffset? changedAtUtc = null)
    {
        TransitionTo(DocumentStatus.Uploaded, NormalizeChangedAtUtc(changedAtUtc));
        FailureReason = null;
        FailureCategory = null;
        _chunks.Clear();
    }

    private void TransitionTo(DocumentStatus nextStatus, DateTimeOffset changedAtUtc)
    {
        if (!IsValidTransition(Status, nextStatus))
        {
            throw new InvalidOperationException(
                $"Invalid status transition from {Status} to {nextStatus}.");
        }

        Status = nextStatus;
        UpdatedAtUtc = changedAtUtc;

        if (nextStatus != DocumentStatus.Failed)
        {
            FailureReason = null;
        }

        if (nextStatus != DocumentStatus.Failed && nextStatus != DocumentStatus.Uploaded)
        {
            FailureCategory = null;
        }
    }

    private static bool IsValidTransition(DocumentStatus current, DocumentStatus next)
    {
        return (current, next) switch
        {
            (DocumentStatus.Uploaded, DocumentStatus.Processing) => true,
            (DocumentStatus.Uploaded, DocumentStatus.Failed) => true,
            (DocumentStatus.Processing, DocumentStatus.Indexed) => true,
            (DocumentStatus.Processing, DocumentStatus.Failed) => true,
            (DocumentStatus.Processing, DocumentStatus.Uploaded) => true,
            (DocumentStatus.Failed, DocumentStatus.Processing) => true,
            _ => false
        };
    }

    private void AdvanceProcessingStage(
        LastProcessingStage lastProcessingStage,
        DateTimeOffset changedAtUtc,
        bool updateTimestamp)
    {
        if (lastProcessingStage < LastProcessingStage)
        {
            throw new InvalidOperationException(
                $"Processing stage cannot move backwards from {LastProcessingStage} to {lastProcessingStage}.");
        }

        LastProcessingStage = lastProcessingStage;
        LastProcessingStageAtUtc = changedAtUtc;

        if (updateTimestamp)
        {
            UpdatedAtUtc = changedAtUtc;
        }
    }

    private void ValidateState()
    {
        if (FailureCategory == FailureCategoryType.Cancelled && Status != DocumentStatus.Uploaded)
        {
            throw new ArgumentException("Cancelled documents must be returned to Uploaded status.");
        }

        if (Status == DocumentStatus.Failed)
        {
            if (FailureCategory is null)
            {
                throw new ArgumentException("Failed documents must include a failure category.");
            }

            if (FailureCategory.Value == FailureCategoryType.Cancelled)
            {
                throw new ArgumentException("Cancelled documents must not be marked as Failed.");
            }

            if (FailureReason is null)
            {
                throw new ArgumentException("Failed documents must include a failure reason.");
            }
        }
        else if (FailureReason is not null)
        {
            throw new ArgumentException("Only failed documents can include a failure reason.");
        }
    }

    private static DateTimeOffset NormalizeChangedAtUtc(DateTimeOffset? changedAtUtc)
    {
        return (changedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    private static string RequireStorageRelativePath(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            ? value.Trim().Replace('\\', '/')
            : throw new ArgumentException("Storage relative path is required.", nameof(value));
    }

    private static string? NormalizeStorageRelativePath(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Replace('\\', '/');
    }
}
