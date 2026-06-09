using DocuMind.Core.Documents;

namespace DocuMind.Core.Tests.Messaging;

public sealed class DocumentIngestionRequestTests
{
    [Fact]
    public void Constructor_ShouldNormalizeStoragePath()
    {
        var request = new DocumentIngestionRequest(
            Guid.NewGuid(),
            "guide.md",
            "text/markdown",
            64,
            @"uploads\guide.md",
            DateTimeOffset.UtcNow);

        Assert.Equal("uploads/guide.md", request.StorageRelativePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_ShouldRejectMissingFileName(string fileName)
    {
        var exception = Assert.Throws<ArgumentException>(() => new DocumentIngestionRequest(
            Guid.NewGuid(),
            fileName,
            "application/pdf",
            64,
            "uploads/file.pdf",
            DateTimeOffset.UtcNow));

        Assert.Equal("fileName", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyDocumentId()
    {
        var exception = Assert.Throws<ArgumentException>(() => new DocumentIngestionRequest(
            Guid.Empty,
            "file.pdf",
            "application/pdf",
            64,
            "uploads/file.pdf",
            DateTimeOffset.UtcNow));

        Assert.Equal("documentId", exception.ParamName);
    }
}
