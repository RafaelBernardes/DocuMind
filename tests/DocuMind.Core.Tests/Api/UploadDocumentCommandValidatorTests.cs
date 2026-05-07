using DocuMind.Api.Documents.Commands.UploadDocument;
using DocuMind.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;

namespace DocuMind.Core.Tests.Api;

public sealed class UploadDocumentCommandValidatorTests
{
    private readonly UploadDocumentCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldRejectMissingFile()
    {
        var result = _validator.Validate(file: null, CreateOptions());

        Assert.False(result.IsValid);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("missing_file", result.ErrorCode);
    }

    [Fact]
    public void Validate_ShouldRejectUnsupportedExtension()
    {
        var file = CreateFormFile("report.exe", "application/octet-stream", 10);

        var result = _validator.Validate(file, CreateOptions());

        Assert.False(result.IsValid);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, result.StatusCode);
        Assert.Equal("unsupported_extension", result.ErrorCode);
    }

    [Fact]
    public void Validate_ShouldRejectFileAboveConfiguredLimit()
    {
        var file = CreateFormFile("report.pdf", "application/pdf", 3 * 1024 * 1024);

        var result = _validator.Validate(file, CreateOptions(maxFileSizeMb: 2));

        Assert.False(result.IsValid);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, result.StatusCode);
        Assert.Equal("file_too_large", result.ErrorCode);
    }

    private static IngestionOptions CreateOptions(int maxFileSizeMb = 25)
    {
        return new IngestionOptions
        {
            MaxFileSizeMb = maxFileSizeMb,
            AllowedExtensions = [".pdf", ".md", ".txt"]
        };
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, int sizeInBytes)
    {
        var content = new byte[sizeInBytes];
        var stream = new MemoryStream(content);

        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
