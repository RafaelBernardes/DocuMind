namespace DocuMind.Core.Documents;

public sealed class Chunk
{
    public Chunk(Guid id, Guid documentId, int order, string content, ChunkMetadata metadata)
    {
        Id = id != Guid.Empty ? id : throw new ArgumentException("Chunk id is required.", nameof(id));
        DocumentId = documentId != Guid.Empty
            ? documentId
            : throw new ArgumentException("Document id is required.", nameof(documentId));
        Order = order >= 0 ? order : throw new ArgumentOutOfRangeException(nameof(order));
        Content = !string.IsNullOrWhiteSpace(content)
            ? content.Trim()
            : throw new ArgumentException("Chunk content is required.", nameof(content));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public Guid Id { get; }

    public Guid DocumentId { get; }

    public int Order { get; }

    public string Content { get; }

    public ChunkMetadata Metadata { get; }
}
