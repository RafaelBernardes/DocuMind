using DocuMind.Core.Documents;

namespace DocuMind.Core.Tests.Documents;

public class DocumentTests
{
    [Fact]
    public void Constructor_ShouldInitializeUploadedDocument()
    {
        var uploadedAt = new DateTimeOffset(2026, 04, 29, 12, 00, 00, TimeSpan.Zero);
        var metadata = new DocumentMetadata("invoice.pdf", "application/pdf", 1024, "sha256");

        var document = new Document(Guid.NewGuid(), metadata, "uploads/documents/invoice.pdf", uploadedAt);

        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        Assert.Equal(uploadedAt, document.UploadedAtUtc);
        Assert.Equal(uploadedAt, document.UpdatedAtUtc);
        Assert.Equal("uploads/documents/invoice.pdf", document.StorageRelativePath);
        Assert.Empty(document.Chunks);
        Assert.Null(document.FailureReason);
    }

    [Fact]
    public void Constructor_ShouldRequireValidMetadataFields()
    {
        Assert.Throws<ArgumentException>(() => new DocumentMetadata("", "application/pdf", 100));
        Assert.Throws<ArgumentException>(() => new DocumentMetadata("file.pdf", "", 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentMetadata("file.pdf", "application/pdf", 0));
        Assert.Throws<ArgumentException>(() => new Document(Guid.NewGuid(), new DocumentMetadata("file.pdf", "application/pdf", 100), ""));
    }

    [Fact]
    public void Rehydrate_ShouldAllowMissingLegacyStoragePath()
    {
        var document = Document.Rehydrate(
            Guid.NewGuid(),
            new DocumentMetadata("legacy.pdf", "application/pdf", 100),
            storageRelativePath: null,
            DocumentStatus.Uploaded,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            failureReason: null,
            chunks: []);

        Assert.Null(document.StorageRelativePath);
    }

    [Fact]
    public void MarkProcessing_ShouldTransitionFromUploadedToProcessing()
    {
        var document = CreateDocument();

        document.MarkProcessing();

        Assert.Equal(DocumentStatus.Processing, document.Status);
        Assert.Null(document.FailureReason);
    }

    [Fact]
    public void MarkIndexed_ShouldRequireChunksAndStoreThem()
    {
        var document = CreateDocument();
        document.MarkProcessing();

        var chunk = new Chunk(
            Guid.NewGuid(),
            document.Id,
            0,
            "chunk body",
            new ChunkMetadata(characterCount: 10, tokenCount: 3, pageLabel: "1"));

        document.MarkIndexed([chunk]);

        Assert.Equal(DocumentStatus.Indexed, document.Status);
        Assert.Single(document.Chunks);
        Assert.Equal(chunk, document.Chunks.Single());
    }

    [Fact]
    public void MarkIndexed_ShouldRejectChunksFromAnotherDocument()
    {
        var document = CreateDocument();
        document.MarkProcessing();

        var foreignChunk = new Chunk(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "chunk body",
            new ChunkMetadata(characterCount: 10));

        var exception = Assert.Throws<ArgumentException>(() => document.MarkIndexed([foreignChunk]));

        Assert.Contains("belong to the document", exception.Message);
    }

    [Fact]
    public void MarkIndexed_ShouldRejectNullChunkEntries()
    {
        var document = CreateDocument();
        document.MarkProcessing();

        var exception = Assert.Throws<ArgumentException>(() => document.MarkIndexed([null!]));

        Assert.Contains("cannot contain null", exception.Message);
    }

    [Fact]
    public void MarkFailed_ShouldRequireReason()
    {
        var document = CreateDocument();

        Assert.Throws<ArgumentException>(() => document.MarkFailed(" "));
    }

    [Fact]
    public void MarkIndexed_ShouldRejectInvalidTransition()
    {
        var document = CreateDocument();
        var chunk = new Chunk(
            Guid.NewGuid(),
            document.Id,
            0,
            "chunk body",
            new ChunkMetadata(characterCount: 10));

        var exception = Assert.Throws<InvalidOperationException>(() => document.MarkIndexed([chunk]));

        Assert.Contains("Uploaded", exception.Message);
        Assert.Contains("Indexed", exception.Message);
    }

    [Fact]
    public void FailedDocument_ShouldAllowRetryProcessing()
    {
        var document = CreateDocument();
        document.MarkFailed("OCR timeout");

        document.MarkProcessing();

        Assert.Equal(DocumentStatus.Processing, document.Status);
        Assert.Null(document.FailureReason);
    }

    [Fact]
    public void MarkProcessing_ShouldRejectTransitionToSameState()
    {
        var document = CreateDocument();
        document.MarkProcessing();

        var exception = Assert.Throws<InvalidOperationException>(() => document.MarkProcessing());

        Assert.Contains("Processing", exception.Message);
    }

    private static Document CreateDocument()
    {
        return new Document(
            Guid.NewGuid(),
            new DocumentMetadata("invoice.pdf", "application/pdf", 1024),
            "uploads/documents/invoice.pdf");
    }
}
