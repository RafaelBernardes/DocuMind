namespace DocuMind.Core.Storage;

public sealed record StoredFile(string RelativePath, long SizeInBytes)
{
    public string RelativePath { get; init; } = RequirePath(RelativePath);
    public long SizeInBytes { get; init; } = SizeInBytes >= 0
        ? SizeInBytes
        : throw new ArgumentOutOfRangeException(nameof(SizeInBytes), "Size cannot be negative.");

    private static string RequirePath(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            ? value.Trim().Replace('\\', '/')
            : throw new ArgumentException("Relative path is required.", nameof(value));
    }
}
