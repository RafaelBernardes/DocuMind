using DocuMind.Core.Storage;
using DocuMind.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;
    private readonly string _uploadsPath;
    private readonly string _processedPath;

    public LocalFileStorage(
        IOptions<LocalStorageOptions> options,
        IServiceProvider serviceProvider)
    {
        var storageOptions = options?.Value ?? throw new ArgumentNullException(nameof(options));
        var contentRootPath = serviceProvider.GetService(typeof(IHostEnvironment)) is IHostEnvironment hostEnvironment
            ? hostEnvironment.ContentRootPath
            : Directory.GetCurrentDirectory();

        _basePath = Path.GetFullPath(Path.Combine(contentRootPath, NormalizeRelativePath(storageOptions.BasePath)));
        _uploadsPath = NormalizeRelativePath(storageOptions.UploadsPath);
        _processedPath = NormalizeRelativePath(storageOptions.ProcessedPath);

        if (PathsOverlap(_uploadsPath, _processedPath))
        {
            throw new ArgumentException(
                "UploadsPath and ProcessedPath must be separate non-overlapping directories.",
                nameof(options));
        }
    }

    public async Task<StoredFile> SaveUploadAsync(
        Guid documentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document id is required.", nameof(documentId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
        {
            throw new ArgumentException("Content stream must be readable.", nameof(content));
        }

        var sanitizedFileName = SanitizeFileName(fileName);
        var relativePath = CombineRelative(_uploadsPath, documentId.ToString("N"), sanitizedFileName);
        var fullPath = ResolveFullPath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var fileStream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var fileInfo = new FileInfo(fullPath);
        return new StoredFile(relativePath, fileInfo.Length);
    }

    public Task<Stream> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolveFullPath(relativePath);
        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous);

        return Task.FromResult(stream);
    }

    public Task<StoredFile> MoveToProcessedAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = NormalizeRelativePath(relativePath);
        var uploadsPrefix = _uploadsPath + "/";
        if (!normalizedPath.StartsWith(uploadsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Only files stored in the uploads directory can be moved to processed.",
                nameof(relativePath));
        }

        var sourceFullPath = ResolveFullPath(normalizedPath);
        var relativeSubPath = normalizedPath[uploadsPrefix.Length..];
        var processedRelativePath = CombineRelative(_processedPath, relativeSubPath);
        var destinationFullPath = ResolveFullPath(processedRelativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationFullPath)!);
        File.Move(sourceFullPath, destinationFullPath, overwrite: false);

        var fileInfo = new FileInfo(destinationFullPath);
        return Task.FromResult(new StoredFile(processedRelativePath, fileInfo.Length));
    }

    public Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolveFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolveFullPath(string relativePath)
    {
        var normalizedPath = NormalizeRelativePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, normalizedPath));

        if (!IsWithinStorageRoot(fullPath))
        {
            throw new InvalidOperationException("Resolved path escaped the configured storage root.");
        }

        return fullPath;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path is required.", nameof(relativePath));
        }

        var normalizedPath = relativePath.Trim().Replace('\\', '/');
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0
            || Path.IsPathRooted(normalizedPath)
            || normalizedPath.StartsWith('/')
            || normalizedPath.StartsWith('\\')
            || segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Relative path must stay within the configured storage root.", nameof(relativePath));
        }

        return string.Join('/', segments);
    }

    private static string SanitizeFileName(string fileName)
    {
        var originalFileName = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(originalFileName)
            || originalFileName is "." or "..")
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        var invalidFileNameChars = Path.GetInvalidFileNameChars();
        var sanitizedCharacters = originalFileName
            .Select(character => invalidFileNameChars.Contains(character) ? '_' : character)
            .ToArray();

        return new string(sanitizedCharacters);
    }

    private static string CombineRelative(params string[] segments)
    {
        return string.Join(
            '/',
            segments
                .Select(NormalizeRelativePath)
                .SelectMany(segment => segment.Split('/', StringSplitOptions.RemoveEmptyEntries)));
    }

    private static bool PathsOverlap(string firstPath, string secondPath)
    {
        return IsSameOrNestedPath(firstPath, secondPath)
            || IsSameOrNestedPath(secondPath, firstPath);
    }

    private static bool IsSameOrNestedPath(string parentPath, string candidatePath)
    {
        return candidatePath.Equals(parentPath, StringComparison.OrdinalIgnoreCase)
            || candidatePath.StartsWith(parentPath + '/', StringComparison.OrdinalIgnoreCase);
    }

    private bool IsWithinStorageRoot(string fullPath)
    {
        if (fullPath.Equals(_basePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = _basePath.EndsWith(Path.DirectorySeparatorChar)
            || _basePath.EndsWith(Path.AltDirectorySeparatorChar)
            ? _basePath
            : _basePath + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
