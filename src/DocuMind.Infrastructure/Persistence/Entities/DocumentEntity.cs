using DocuMind.Core.Documents;

namespace DocuMind.Infrastructure.Persistence.Entities;

public sealed class DocumentEntity
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeInBytes { get; set; }

    public string? Checksum { get; set; }

    public string? StorageRelativePath { get; set; }

    public DocumentStatus Status { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string? FailureReason { get; set; }

    public ICollection<DocumentChunkEntity> Chunks { get; set; } = [];
}
