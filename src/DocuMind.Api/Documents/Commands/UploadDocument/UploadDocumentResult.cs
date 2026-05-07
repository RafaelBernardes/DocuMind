namespace DocuMind.Api.Documents.Commands.UploadDocument;

public sealed record UploadDocumentResult(
    Guid Id,
    string Status,
    string FileName,
    string ContentType,
    long SizeInBytes,
    DateTimeOffset UploadedAtUtc);
