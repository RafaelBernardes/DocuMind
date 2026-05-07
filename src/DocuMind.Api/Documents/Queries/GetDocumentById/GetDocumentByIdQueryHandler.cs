using DocuMind.Core.Documents;

namespace DocuMind.Api.Documents.Queries.GetDocumentById;

public sealed class GetDocumentByIdQueryHandler(IDocumentRepository documentRepository)
{
    public async Task<GetDocumentByIdOperationResult> HandleAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return GetDocumentByIdOperationResult.NotFound(id);
        }

        return GetDocumentByIdOperationResult.Success(new GetDocumentByIdResult(
            document.Id,
            document.Status.ToString(),
            document.Metadata.FileName,
            document.Metadata.ContentType,
            document.Metadata.SizeInBytes,
            document.UploadedAtUtc,
            document.UpdatedAtUtc,
            document.FailureReason));
    }
}

public sealed record GetDocumentByIdResult(
    Guid Id,
    string Status,
    string FileName,
    string ContentType,
    long SizeInBytes,
    DateTimeOffset UploadedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? FailureReason);

public sealed record GetDocumentByIdOperationResult(
    bool IsSuccess,
    GetDocumentByIdResult? Document,
    int? StatusCode,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static GetDocumentByIdOperationResult Success(GetDocumentByIdResult document)
    {
        return new GetDocumentByIdOperationResult(true, document, null, null, null);
    }

    public static GetDocumentByIdOperationResult NotFound(Guid id)
    {
        return new GetDocumentByIdOperationResult(
            false,
            null,
            StatusCodes.Status404NotFound,
            "document_not_found",
            $"Document '{id}' was not found.");
    }
}
