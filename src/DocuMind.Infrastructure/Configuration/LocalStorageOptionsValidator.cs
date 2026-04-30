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

        if (!IsRelativeToBasePath(options.BasePath, options.UploadsPath))
        {
            failures.Add("LocalStorage:UploadsPath must be a relative path under BasePath.");
        }

        if (!IsRelativeToBasePath(options.BasePath, options.ProcessedPath))
        {
            failures.Add("LocalStorage:ProcessedPath must be a relative path under BasePath.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsRelativeToBasePath(string basePath, string candidatePath)
    {
        var normalizedBasePath = Normalize(basePath);
        var normalizedCandidatePath = Normalize(candidatePath);
        var segments = normalizedCandidatePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (IsAbsolutePathLike(candidatePath))
        {
            return false;
        }

        if (segments.Any(segment => segment is "." or ".."))
        {
            return false;
        }

        return !normalizedCandidatePath.Equals(normalizedBasePath, StringComparison.OrdinalIgnoreCase)
            && !normalizedCandidatePath.StartsWith(
                normalizedBasePath + '/',
                StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        return path
            .Trim()
            .Replace('\\', '/')
            .TrimEnd('/');
    }

    private static bool IsAbsolutePathLike(string path)
    {
        var trimmedPath = path.Trim();

        if (Path.IsPathRooted(trimmedPath))
        {
            return true;
        }

        if (trimmedPath.StartsWith('/') || trimmedPath.StartsWith('\\'))
        {
            return true;
        }

        return trimmedPath.Length >= 3
            && char.IsAsciiLetter(trimmedPath[0])
            && trimmedPath[1] == ':'
            && (trimmedPath[2] == '/' || trimmedPath[2] == '\\');
    }
}
