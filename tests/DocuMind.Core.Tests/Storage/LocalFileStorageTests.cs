using System.Text;
using DocuMind.Core.Storage;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocuMind.Core.Tests.Storage;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _contentRootPath = Path.Combine(
        Path.GetTempPath(),
        "documind-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveUploadAsync_ShouldPersistFileUnderUploadsDirectory()
    {
        Directory.CreateDirectory(_contentRootPath);
        var storage = CreateStorage();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello storage"));

        var storedFile = await storage.SaveUploadAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "contract.pdf",
            content);

        Assert.Equal("uploads/11111111111111111111111111111111/contract.pdf", storedFile.RelativePath);
        Assert.Equal(content.Length, storedFile.SizeInBytes);
        Assert.True(File.Exists(Path.Combine(_contentRootPath, "storage", "uploads", "11111111111111111111111111111111", "contract.pdf")));
    }

    [Fact]
    public async Task OpenReadAsync_ShouldReturnPersistedContent()
    {
        Directory.CreateDirectory(_contentRootPath);
        var storage = CreateStorage();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("storage content"));

        var storedFile = await storage.SaveUploadAsync(Guid.NewGuid(), "notes.txt", content);
        await using var stream = await storage.OpenReadAsync(storedFile.RelativePath);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var text = await reader.ReadToEndAsync();

        Assert.Equal("storage content", text);
    }

    [Fact]
    public async Task MoveToProcessedAsync_ShouldMoveFilePreservingDocumentSubpath()
    {
        Directory.CreateDirectory(_contentRootPath);
        var storage = CreateStorage();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("processed content"));

        var storedFile = await storage.SaveUploadAsync(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "manual.md",
            content);

        var movedFile = await storage.MoveToProcessedAsync(storedFile.RelativePath);

        Assert.Equal("processed/22222222222222222222222222222222/manual.md", movedFile.RelativePath);
        Assert.False(File.Exists(Path.Combine(_contentRootPath, "storage", "uploads", "22222222222222222222222222222222", "manual.md")));
        Assert.True(File.Exists(Path.Combine(_contentRootPath, "storage", "processed", "22222222222222222222222222222222", "manual.md")));
    }

    [Fact]
    public async Task MoveToProcessedAsync_ShouldAllowConfiguredPathsWithTrailingSlashes()
    {
        Directory.CreateDirectory(_contentRootPath);
        var storage = CreateStorage(
            basePath: "storage/",
            uploadsPath: "uploads/",
            processedPath: "processed/");
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("processed content"));

        var storedFile = await storage.SaveUploadAsync(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "guide.md",
            content);

        var movedFile = await storage.MoveToProcessedAsync(storedFile.RelativePath);

        Assert.Equal("processed/33333333333333333333333333333333/guide.md", movedFile.RelativePath);
        Assert.True(File.Exists(Path.Combine(_contentRootPath, "storage", "processed", "33333333333333333333333333333333", "guide.md")));
    }

    [Fact]
    public async Task MoveToProcessedAsync_ShouldRejectPathsOutsideUploadsDirectory()
    {
        Directory.CreateDirectory(_contentRootPath);
        var storage = CreateStorage();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => storage.MoveToProcessedAsync("processed/existing/file.txt"));

        Assert.Equal("relativePath", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldRejectOverlappingStorageDirectories()
    {
        Directory.CreateDirectory(_contentRootPath);

        var exception = Assert.Throws<ArgumentException>(() => CreateStorage(
            uploadsPath: "uploads",
            processedPath: "uploads/processed"));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public async Task OpenReadAsync_ShouldRejectPathTraversal()
    {
        Directory.CreateDirectory(_contentRootPath);
        var storage = CreateStorage();

        await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync("../secrets.txt"));
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemovePersistedFile()
    {
        Directory.CreateDirectory(_contentRootPath);
        var storage = CreateStorage();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("delete me"));

        var storedFile = await storage.SaveUploadAsync(Guid.NewGuid(), "notes.txt", content);
        await storage.DeleteAsync(storedFile.RelativePath);

        Assert.False(File.Exists(Path.Combine(_contentRootPath, "storage", storedFile.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRootPath))
        {
            Directory.Delete(_contentRootPath, recursive: true);
        }
    }

    private IFileStorage CreateStorage(
        string basePath = "storage",
        string uploadsPath = "uploads",
        string processedPath = "processed")
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(_contentRootPath));

        var provider = services.BuildServiceProvider();
        return new LocalFileStorage(
            Options.Create(new LocalStorageOptions
            {
                BasePath = basePath,
                UploadsPath = uploadsPath,
                ProcessedPath = processedPath
            }),
            provider);
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "DocuMind.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
