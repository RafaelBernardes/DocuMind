using Pgvector;

namespace DocuMind.Infrastructure.Persistence.Entities;

public sealed class DocumentChunkEntity
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public int Order { get; set; }

    public string Content { get; set; } = string.Empty;

    public int CharacterCount { get; set; }

    public int? TokenCount { get; set; }

    public string? PageLabel { get; set; }

    public Vector? Embedding { get; set; }

    public DocumentEntity Document { get; set; } = null!;
}
