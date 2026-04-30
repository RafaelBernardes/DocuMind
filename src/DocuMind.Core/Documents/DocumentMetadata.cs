namespace DocuMind.Core.Documents;

public sealed record DocumentMetadata(
    string FileName,
    string ContentType,
    long SizeInBytes,
    string? Checksum = null)
{
    public string FileName { get; init; } = Require(FileName, nameof(FileName));
    public string ContentType { get; init; } = Require(ContentType, nameof(ContentType));
    public long SizeInBytes { get; init; } = SizeInBytes > 0
        ? SizeInBytes
        : throw new ArgumentOutOfRangeException(nameof(SizeInBytes), "Size must be greater than zero.");

    private static string Require(string value, string paramName)
    {
        return !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException("Value is required.", paramName);
    }
}
