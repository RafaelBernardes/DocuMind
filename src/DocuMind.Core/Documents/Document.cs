namespace DocuMind.Core.Documents;

public sealed class Document
{
    private readonly List<Chunk> _chunks = [];

    public Document(Guid id, DocumentMetadata metadata, DateTimeOffset? uploadedAtUtc = null)
        : this(
            id,
            metadata,
            DocumentStatus.Uploaded,
            uploadedAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
            uploadedAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
            failureReason: null,
            chunks: null)
    {
    }

    private Document(
        Guid id,
        DocumentMetadata metadata,
        DocumentStatus status,
        DateTimeOffset uploadedAtUtc,
        DateTimeOffset updatedAtUtc,
        string? failureReason,
        IEnumerable<Chunk>? chunks)
    {
        Id = id != Guid.Empty ? id : throw new ArgumentException("Document id is required.", nameof(id));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Status = status;
        UploadedAtUtc = uploadedAtUtc.ToUniversalTime();
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();

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
        DocumentStatus status,
        DateTimeOffset uploadedAtUtc,
        DateTimeOffset updatedAtUtc,
        string? failureReason,
        IEnumerable<Chunk>? chunks)
    {
        return new Document(id, metadata, status, uploadedAtUtc, updatedAtUtc, failureReason, chunks);
    }

    public Guid Id { get; }

    public DocumentMetadata Metadata { get; }

    public DocumentStatus Status { get; private set; }

    public DateTimeOffset UploadedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string? FailureReason { get; private set; }

    public IReadOnlyCollection<Chunk> Chunks => _chunks.AsReadOnly();

    public void MarkProcessing(DateTimeOffset? changedAtUtc = null)
    {
        TransitionTo(DocumentStatus.Processing, changedAtUtc);
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

        TransitionTo(DocumentStatus.Indexed, changedAtUtc);

        _chunks.Clear();
        _chunks.AddRange(materializedChunks);
        FailureReason = null;
    }

    public void MarkFailed(string reason, DateTimeOffset? changedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(reason));
        }

        TransitionTo(DocumentStatus.Failed, changedAtUtc);
        FailureReason = reason.Trim();
    }

    private void TransitionTo(DocumentStatus nextStatus, DateTimeOffset? changedAtUtc)
    {
        if (!IsValidTransition(Status, nextStatus))
        {
            throw new InvalidOperationException(
                $"Invalid status transition from {Status} to {nextStatus}.");
        }

        Status = nextStatus;
        UpdatedAtUtc = (changedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();

        if (nextStatus != DocumentStatus.Failed)
        {
            FailureReason = null;
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
            (DocumentStatus.Failed, DocumentStatus.Processing) => true,
            _ => false
        };
    }
}
