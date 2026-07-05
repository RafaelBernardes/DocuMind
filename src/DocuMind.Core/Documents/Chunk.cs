namespace DocuMind.Core.Documents;

public sealed class Chunk
{
    public Chunk(
        Guid id,
        Guid documentId,
        int order,
        string content,
        ChunkMetadata metadata,
        IReadOnlyList<float>? embedding = null)
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
        Embedding = CopyEmbedding(embedding);
    }

    public Guid Id { get; }

    public Guid DocumentId { get; }

    public int Order { get; }

    public string Content { get; }

    public ChunkMetadata Metadata { get; }

    public IReadOnlyList<float>? Embedding { get; }

    private static IReadOnlyList<float>? CopyEmbedding(IReadOnlyList<float>? embedding)
    {
        if (embedding is null)
        {
            return null;
        }

        if (embedding.Count != EmbeddingConstants.ExpectedDimensions)
        {
            throw new ArgumentException(
                $"Chunk embedding must have exactly {EmbeddingConstants.ExpectedDimensions} dimensions.",
                nameof(embedding));
        }

        return embedding.ToArray();
    }
}
