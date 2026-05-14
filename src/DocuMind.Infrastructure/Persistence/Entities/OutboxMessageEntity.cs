namespace DocuMind.Infrastructure.Persistence.Entities;

public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public Guid DocumentId { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public DateTimeOffset? ProcessedAtUtc { get; set; }

    public string? Error { get; set; }
}
