namespace DocuMind.Core.Storage;

public interface IFileStorage
{
    Task<StoredFile> SaveUploadAsync(
        Guid documentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<StoredFile> MoveToProcessedAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}
