using System.Security.Cryptography;
using System.Text;
using DocuMind.Core.Documents;
using DocuMind.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Chunking;

public sealed class SimpleDeterministicDocumentChunker : IDocumentChunker
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;

    public SimpleDeterministicDocumentChunker(IOptions<IngestionOptions> options)
    {
        var ingestionOptions = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _chunkSize = ingestionOptions.ChunkSize;
        _chunkOverlap = ingestionOptions.ChunkOverlap;
    }

    public IReadOnlyList<Chunk> Chunk(Guid documentId, string text)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document id is required.", nameof(documentId));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text is required.", nameof(text));
        }

        var normalizedText = NormalizeText(text);
        var chunks = new List<Chunk>();
        var start = 0;
        var order = 0;

        while (start < normalizedText.Length)
        {
            var end = DetermineChunkEnd(normalizedText, start);
            var content = normalizedText[start..end].Trim();
            if (content.Length == 0)
            {
                start = Math.Min(start + 1, normalizedText.Length);
                continue;
            }

            chunks.Add(new Chunk(
                CreateDeterministicChunkId(documentId, order, content),
                documentId,
                order,
                content,
                new ChunkMetadata(characterCount: content.Length)));

            if (end >= normalizedText.Length)
            {
                break;
            }

            var nextStart = Math.Max(end - _chunkOverlap, start + 1);
            start = nextStart;
            order++;
        }

        return chunks;
    }

    private int DetermineChunkEnd(string text, int start)
    {
        var maxEnd = Math.Min(start + _chunkSize, text.Length);
        if (maxEnd == text.Length)
        {
            return maxEnd;
        }

        foreach (var separator in Separators)
        {
            var separatorIndex = FindLastSeparator(text, start, maxEnd, separator);
            if (separatorIndex > start)
            {
                return separatorIndex;
            }
        }

        return maxEnd;
    }

    private static int FindLastSeparator(string text, int start, int endExclusive, string separator)
    {
        var searchLength = endExclusive - start;
        var lastIndex = text.LastIndexOf(separator, startIndex: endExclusive - 1, count: searchLength, StringComparison.Ordinal);
        if (lastIndex <= start)
        {
            return -1;
        }

        var candidate = text[start..lastIndex].Trim();
        return candidate.Length == 0 ? -1 : lastIndex;
    }

    private static Guid CreateDeterministicChunkId(Guid documentId, int order, string content)
    {
        var bytes = Encoding.UTF8.GetBytes($"{documentId:N}:{order}:{content}");
        var hash = MD5.HashData(bytes);
        return new Guid(hash);
    }

    private static string NormalizeText(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    private static readonly string[] Separators = ["\n\n", "\n", " "];
}
