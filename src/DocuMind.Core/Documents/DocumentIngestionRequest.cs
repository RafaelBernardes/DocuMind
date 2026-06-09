namespace DocuMind.Core.Documents;

public sealed record DocumentIngestionRequest
{
    public DocumentIngestionRequest(
        Guid documentId,
        string fileName,
        string contentType,
        long sizeInBytes,
        string? storageRelativePath,
        DateTimeOffset uploadedAtUtc)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document id is required.", nameof(documentId));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type is required.", nameof(contentType));
        }

        if (sizeInBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "File size cannot be negative.");
        }

        DocumentId = documentId;
        FileName = fileName.Trim();
        ContentType = contentType.Trim();
        SizeInBytes = sizeInBytes;
        StorageRelativePath = string.IsNullOrWhiteSpace(storageRelativePath)
            ? null
            : storageRelativePath.Trim().Replace('\\', '/');
        UploadedAtUtc = uploadedAtUtc.ToUniversalTime();
    }

    public Guid DocumentId { get; }

    public string FileName { get; }

    public string ContentType { get; }

    public long SizeInBytes { get; }

    public string? StorageRelativePath { get; }

    public DateTimeOffset UploadedAtUtc { get; }
}
