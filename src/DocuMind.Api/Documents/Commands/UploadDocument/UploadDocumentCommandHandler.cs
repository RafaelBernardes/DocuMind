using DocuMind.Core.Documents;
using DocuMind.Core.Storage;
using DocuMind.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DocuMind.Api.Documents.Commands.UploadDocument;

public sealed class UploadDocumentCommandHandler(
    IDocumentRepository documentRepository,
    IFileStorage fileStorage,
    IOptions<IngestionOptions> ingestionOptions,
    UploadDocumentCommandValidator requestValidator)
{
    public async Task<UploadDocumentOperationResult> HandleAsync(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        var validation = requestValidator.Validate(file, ingestionOptions.Value);
        if (!validation.IsValid)
        {
            return UploadDocumentOperationResult.Failure(
                validation.StatusCode!.Value,
                validation.ErrorCode!,
                validation.ErrorMessage!);
        }

        var documentId = Guid.NewGuid();
        await using var contentStream = file!.OpenReadStream();

        var storedFile = await fileStorage.SaveUploadAsync(
            documentId,
            file.FileName,
            contentStream,
            cancellationToken);

        var metadata = new DocumentMetadata(
            Path.GetFileName(file.FileName),
            NormalizeContentType(file.ContentType),
            storedFile.SizeInBytes);

        var document = new Document(documentId, metadata, storedFile.RelativePath);
        try
        {
            await documentRepository.AddAsync(document, cancellationToken);
        }
        catch
        {
            await DeleteStoredFileSafelyAsync(fileStorage, storedFile.RelativePath, cancellationToken);
            throw;
        }

        return UploadDocumentOperationResult.Success(new UploadDocumentResult(
            document.Id,
            document.Status.ToString(),
            document.Metadata.FileName,
            document.Metadata.ContentType,
            document.Metadata.SizeInBytes,
            document.UploadedAtUtc));
    }

    private static string NormalizeContentType(string? contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();
    }

    private static async Task DeleteStoredFileSafelyAsync(
        IFileStorage fileStorage,
        string relativePath,
        CancellationToken cancellationToken)
    {
        try
        {
            await fileStorage.DeleteAsync(relativePath, cancellationToken);
        }
        catch
        {
            // Best effort cleanup to reduce orphaned files if the database write fails.
        }
    }
}

public sealed record UploadDocumentOperationResult(
    bool IsSuccess,
    UploadDocumentResult? Document,
    int? StatusCode,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static UploadDocumentOperationResult Success(UploadDocumentResult document)
    {
        return new UploadDocumentOperationResult(true, document, null, null, null);
    }

    public static UploadDocumentOperationResult Failure(int statusCode, string errorCode, string errorMessage)
    {
        return new UploadDocumentOperationResult(false, null, statusCode, errorCode, errorMessage);
    }
}
