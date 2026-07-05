using DocuMind.Api.Documents.Queries.GetDocumentById;
using DocuMind.Core.Documents;
using Microsoft.AspNetCore.Http;

namespace DocuMind.Core.Tests.Api;

public sealed class GetDocumentByIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldReturnDocumentWhenFound()
    {
        var document = new Document(
            Guid.NewGuid(),
            new DocumentMetadata("manual.pdf", "application/pdf", 1234),
            "uploads/manual.pdf",
            DateTimeOffset.Parse("2026-05-05T12:00:00Z"));
        var repository = new FakeDocumentRepository(document);
        var handler = new GetDocumentByIdQueryHandler(repository);

        var result = await handler.HandleAsync(document.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.Equal(document.Id, result.Document.Id);
        Assert.Equal("Uploaded", result.Document.Status);
        Assert.Equal("manual.pdf", result.Document.FileName);
        Assert.Equal("application/pdf", result.Document.ContentType);
        Assert.Equal(1234, result.Document.SizeInBytes);
        Assert.Equal(document.UploadedAtUtc, result.Document.UploadedAtUtc);
        Assert.Equal(document.UpdatedAtUtc, result.Document.UpdatedAtUtc);
        Assert.Null(result.Document.FailureReason);
        Assert.Equal(LastProcessingStage.None.ToString(), result.Document.LastProcessingStage);
        Assert.Null(result.Document.FailureCategory);
        Assert.Equal(0, result.Document.ProcessingAttemptCount);
        Assert.Null(result.Document.LastProcessingStartedAtUtc);
        Assert.Null(result.Document.LastProcessingStageAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNotFoundWhenMissing()
    {
        var missingId = Guid.NewGuid();
        var repository = new FakeDocumentRepository(null);
        var handler = new GetDocumentByIdQueryHandler(repository);

        var result = await handler.HandleAsync(missingId);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Document);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("document_not_found", result.ErrorCode);
        Assert.Equal($"Document '{missingId}' was not found.", result.ErrorMessage);
    }

    private sealed class FakeDocumentRepository(Document? document) : IDocumentRepository
    {
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(document?.Id == id ? document : null);
        }

        public Task<bool> TryMarkProcessingIfUploadedAsync(
            Guid documentId,
            DateTimeOffset changedAtUtc,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Document document, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
