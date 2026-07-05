using DocuMind.Api.Documents.Commands.UploadDocument;
using DocuMind.Core.Documents;
using DocuMind.Core.Storage;
using DocuMind.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DocuMind.Core.Tests.Api;

public sealed class UploadDocumentCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldPersistUploadedDocument()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorage();
        var handler = CreateHandler(repository, storage);
        var file = CreateFormFile("manual.pdf", "application/pdf", "contract body");

        var result = await handler.HandleAsync(file);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.Equal("Uploaded", result.Document.Status);
        Assert.Equal("manual.pdf", repository.AddedDocument!.Metadata.FileName);
        Assert.Equal("application/pdf", repository.AddedDocument.Metadata.ContentType);
        Assert.Equal(storage.StoredFile.RelativePath, repository.AddedDocument.StorageRelativePath);
        Assert.Equal(repository.AddedDocument.Id, result.Document.Id);
    }

    [Fact]
    public async Task HandleAsync_ShouldDeleteStoredFileWhenPersistenceFails()
    {
        var repository = new FakeDocumentRepository { ThrowOnAdd = true };
        var storage = new FakeFileStorage();
        var handler = CreateHandler(repository, storage);
        var file = CreateFormFile("manual.pdf", "application/pdf", "contract body");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(file));

        Assert.True(storage.DeleteCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectUnsupportedExtensionWithoutPersisting()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorage();
        var handler = CreateHandler(repository, storage);
        var file = CreateFormFile("manual.exe", "application/octet-stream", "binary");

        var result = await handler.HandleAsync(file);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, result.StatusCode);
        Assert.Null(repository.AddedDocument);
        Assert.False(storage.SaveCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectEmptyFileWithoutPersisting()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorage();
        var handler = CreateHandler(repository, storage);
        var file = CreateFormFile("manual.pdf", "application/pdf", string.Empty);

        var result = await handler.HandleAsync(file);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("empty_file", result.ErrorCode);
        Assert.Null(repository.AddedDocument);
        Assert.False(storage.SaveCalled);
    }

    private static UploadDocumentCommandHandler CreateHandler(
        FakeDocumentRepository repository,
        FakeFileStorage storage,
        int maxFileSizeMb = 25)
    {
        return new UploadDocumentCommandHandler(
            repository,
            storage,
            Options.Create(new IngestionOptions
            {
                MaxFileSizeMb = maxFileSizeMb,
                AllowedExtensions = [".pdf", ".md", ".txt"]
            }),
            new UploadDocumentCommandValidator());
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class FakeDocumentRepository : IDocumentRepository
    {
        public Document? AddedDocument { get; private set; }
        public bool ThrowOnAdd { get; init; }

        public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Document?>(null);
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
            if (ThrowOnAdd)
            {
                throw new InvalidOperationException("Simulated persistence failure.");
            }

            AddedDocument = document;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        public bool SaveCalled { get; private set; }
        public bool DeleteCalled { get; private set; }

        public StoredFile StoredFile { get; private set; } = new("uploads/default/file.pdf", 0);

        public async Task<StoredFile> SaveUploadAsync(
            Guid documentId,
            string fileName,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            SaveCalled = true;

            using var memoryStream = new MemoryStream();
            await content.CopyToAsync(memoryStream, cancellationToken);

            StoredFile = new StoredFile(
                $"uploads/{documentId:N}/{Path.GetFileName(fileName)}",
                memoryStream.Length);

            return StoredFile;
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<StoredFile> MoveToProcessedAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            DeleteCalled = true;
            return Task.CompletedTask;
        }
    }
}
