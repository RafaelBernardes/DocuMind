using DocuMind.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;

namespace DocuMind.Api.Documents.Commands.UploadDocument;

public sealed class UploadDocumentCommandValidator
{
    public UploadValidationResult Validate(IFormFile? file, IngestionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (file is null)
        {
            return UploadValidationResult.Failure(
                StatusCodes.Status400BadRequest,
                "missing_file",
                "A file is required in the multipart field 'file'.");
        }

        if (file.Length <= 0)
        {
            return UploadValidationResult.Failure(
                StatusCodes.Status400BadRequest,
                "empty_file",
                "The uploaded file cannot be empty.");
        }

        var extension = Path.GetExtension(file.FileName);
        var normalizedExtension = NormalizeExtension(extension);
        var allowedExtensions = options.AllowedExtensions
            .Select(NormalizeExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!allowedExtensions.Contains(normalizedExtension))
        {
            return UploadValidationResult.Failure(
                StatusCodes.Status415UnsupportedMediaType,
                "unsupported_extension",
                $"Files with extension '{normalizedExtension}' are not supported.");
        }

        var maxFileSizeInBytes = options.MaxFileSizeMb * 1024L * 1024L;
        if (file.Length > maxFileSizeInBytes)
        {
            return UploadValidationResult.Failure(
                StatusCodes.Status413PayloadTooLarge,
                "file_too_large",
                $"The uploaded file exceeds the configured limit of {options.MaxFileSizeMb} MB.");
        }

        return UploadValidationResult.Success();
    }

    private static string NormalizeExtension(string? extension)
    {
        return string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension.Trim().ToLowerInvariant();
    }
}

public sealed record UploadValidationResult(bool IsValid, int? StatusCode, string? ErrorCode, string? ErrorMessage)
{
    public static UploadValidationResult Success()
    {
        return new UploadValidationResult(true, null, null, null);
    }

    public static UploadValidationResult Failure(int statusCode, string errorCode, string errorMessage)
    {
        return new UploadValidationResult(false, statusCode, errorCode, errorMessage);
    }
}
