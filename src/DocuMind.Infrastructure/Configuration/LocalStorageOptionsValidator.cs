using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Configuration;

public sealed class LocalStorageOptionsValidator : IValidateOptions<LocalStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, LocalStorageOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BasePath))
        {
            failures.Add("LocalStorage:BasePath is required.");
        }

        if (string.IsNullOrWhiteSpace(options.UploadsPath))
        {
            failures.Add("LocalStorage:UploadsPath is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ProcessedPath))
        {
            failures.Add("LocalStorage:ProcessedPath is required.");
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        if (!IsSafeRelativePath(options.BasePath))
        {
            failures.Add("LocalStorage:BasePath must be a safe relative path.");
        }

        if (!IsRelativeToBasePath(options.BasePath, options.UploadsPath))
        {
            failures.Add("LocalStorage:UploadsPath must be a relative path under BasePath.");
        }

        if (!IsRelativeToBasePath(options.BasePath, options.ProcessedPath))
        {
            failures.Add("LocalStorage:ProcessedPath must be a relative path under BasePath.");
        }

        if (TryNormalizeRelativePath(options.UploadsPath, out var normalizedUploadsPath)
            && TryNormalizeRelativePath(options.ProcessedPath, out var normalizedProcessedPath)
            && PathsOverlap(normalizedUploadsPath, normalizedProcessedPath))
        {
            failures.Add("LocalStorage:UploadsPath and ProcessedPath must be separate non-overlapping directories.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsSafeRelativePath(string candidatePath)
    {
        return TryNormalizeRelativePath(candidatePath, out _);
    }

    private static bool IsRelativeToBasePath(string basePath, string candidatePath)
    {
        if (!TryNormalizeRelativePath(basePath, out var normalizedBasePath)
            || !TryNormalizeRelativePath(candidatePath, out var normalizedCandidatePath))
        {
            return false;
        }

        return !normalizedCandidatePath.Equals(normalizedBasePath, StringComparison.OrdinalIgnoreCase)
            && !normalizedCandidatePath.StartsWith(
                normalizedBasePath + '/',
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNormalizeRelativePath(string path, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        var trimmedPath = path.Trim().Replace('\\', '/');

        if (Path.IsPathRooted(trimmedPath))
        {
            return false;
        }

        if (trimmedPath.StartsWith('/') || trimmedPath.StartsWith('\\'))
        {
            return false;
        }

        if (trimmedPath.Length >= 3
            && char.IsAsciiLetter(trimmedPath[0])
            && trimmedPath[1] == ':'
            && trimmedPath[2] == '/')
        {
            return false;
        }

        var segments = trimmedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            return false;
        }

        normalizedPath = string.Join('/', segments);
        return true;
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
}
