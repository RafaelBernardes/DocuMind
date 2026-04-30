namespace DocuMind.Core.Documents;

public sealed record ChunkMetadata
{
    public ChunkMetadata(int characterCount, int? tokenCount = null, string? pageLabel = null)
    {
        CharacterCount = characterCount >= 0
            ? characterCount
            : throw new ArgumentOutOfRangeException(nameof(characterCount));

        TokenCount = tokenCount is null or >= 0
            ? tokenCount
            : throw new ArgumentOutOfRangeException(nameof(tokenCount));

        PageLabel = string.IsNullOrWhiteSpace(pageLabel) ? null : pageLabel.Trim();
    }

    public int CharacterCount { get; }

    public int? TokenCount { get; }

    public string? PageLabel { get; }
}
