using DocuMind.Core.Documents;
using DocuMind.Core.Documents.IntegrationEvents;
using DocuMind.Infrastructure.Messaging.DocumentIngestion;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocuMind.Core.Tests.Messaging;

public sealed class DocumentIngestionMessageHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldTranslateTransportMessageIntoPipelineRequest()
    {
        var pipeline = new FakeDocumentIngestionPipeline();
        var handler = new DocumentIngestionMessageHandler(
            pipeline,
            NullLogger<DocumentIngestionMessageHandler>.Instance);
        var uploadedAtUtc = DateTimeOffset.UtcNow;
        var message = new DocumentUploadedMessage(
            Guid.NewGuid(),
            "contract.pdf",
            "application/pdf",
            1024,
            "uploads/contract.pdf",
            uploadedAtUtc);

        await handler.HandleAsync(message);

        var request = Assert.Single(pipeline.Requests);
        Assert.Equal(message.DocumentId, request.DocumentId);
        Assert.Equal(message.FileName, request.FileName);
        Assert.Equal(message.ContentType, request.ContentType);
        Assert.Equal(message.SizeInBytes, request.SizeInBytes);
        Assert.Equal("uploads/contract.pdf", request.StorageRelativePath);
        Assert.Equal(uploadedAtUtc, request.UploadedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ShouldPropagateCancellationTokenToPipeline()
    {
        var pipeline = new FakeDocumentIngestionPipeline();
        var handler = new DocumentIngestionMessageHandler(
            pipeline,
            NullLogger<DocumentIngestionMessageHandler>.Instance);
        using var cancellationTokenSource = new CancellationTokenSource();
        var message = new DocumentUploadedMessage(
            Guid.NewGuid(),
            "contract.pdf",
            "application/pdf",
            1024,
            "uploads/contract.pdf",
            DateTimeOffset.UtcNow);

        cancellationTokenSource.Cancel();

        await handler.HandleAsync(message, cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, pipeline.CapturedCancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ShouldPropagatePipelineExceptions()
    {
        var pipeline = new FakeDocumentIngestionPipeline
        {
            ExceptionToThrow = new InvalidOperationException("pipeline failed")
        };
        var handler = new DocumentIngestionMessageHandler(
            pipeline,
            NullLogger<DocumentIngestionMessageHandler>.Instance);
        var message = new DocumentUploadedMessage(
            Guid.NewGuid(),
            "contract.pdf",
            "application/pdf",
            1024,
            "uploads/contract.pdf",
            DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(message));

        Assert.Equal("pipeline failed", exception.Message);
        Assert.Single(pipeline.Requests);
    }

    private sealed class FakeDocumentIngestionPipeline : IDocumentIngestionPipeline
    {
        public List<DocumentIngestionRequest> Requests { get; } = [];

        public CancellationToken CapturedCancellationToken { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public Task ProcessAsync(DocumentIngestionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            CapturedCancellationToken = cancellationToken;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }
}
