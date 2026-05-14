namespace DocuMind.Core.Documents.IntegrationEvents;

public sealed record DocumentUploadedMessage(
    Guid DocumentId,
    string FileName,
    string ContentType,
    long SizeInBytes,
    string? StorageRelativePath,
    DateTimeOffset UploadedAtUtc);
