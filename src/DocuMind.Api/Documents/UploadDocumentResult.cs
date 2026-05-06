namespace DocuMind.Api.Documents;

public sealed record UploadDocumentResult(
    Guid Id,
    string Status,
    string FileName,
    string ContentType,
    long SizeInBytes,
    DateTimeOffset UploadedAtUtc);
