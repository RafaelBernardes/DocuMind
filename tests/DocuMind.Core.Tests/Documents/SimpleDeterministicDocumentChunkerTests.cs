using DocuMind.Core.Documents;
using DocuMind.Infrastructure.Chunking;
using DocuMind.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace DocuMind.Core.Tests.Documents;

public sealed class SimpleDeterministicDocumentChunkerTests
{
    [Fact]
    public void Chunk_ShouldReturnSingleChunkWhenTextFits()
    {
        var documentId = Guid.NewGuid();
        var chunker = CreateChunker(chunkSize: 50, chunkOverlap: 10);

        var chunks = chunker.Chunk(documentId, "short text");

        var chunk = Assert.Single(chunks);
        Assert.Equal(documentId, chunk.DocumentId);
        Assert.Equal(0, chunk.Order);
        Assert.Equal("short text", chunk.Content);
        Assert.Equal(10, chunk.Metadata.CharacterCount);
        Assert.Null(chunk.Metadata.TokenCount);
        Assert.Null(chunk.Metadata.PageLabel);
    }

    [Fact]
    public void Chunk_ShouldCreateMultipleChunksWithOverlap()
    {
        var chunker = CreateChunker(chunkSize: 10, chunkOverlap: 3);

        var chunks = chunker.Chunk(Guid.NewGuid(), "abcdefghijKLMNOPQRST");

        Assert.Equal(3, chunks.Count);
        Assert.Equal("abcdefghij", chunks[0].Content);
        Assert.Equal("hijKLMNOPQ", chunks[1].Content);
        Assert.Equal("OPQRST", chunks[2].Content);
    }

    [Fact]
    public void Chunk_ShouldPreferSeparatorBeforeHardCut()
    {
        var chunker = CreateChunker(chunkSize: 15, chunkOverlap: 2);

        var chunks = chunker.Chunk(Guid.NewGuid(), "alpha beta gamma delta");

        Assert.Equal("alpha beta", chunks[0].Content);
        Assert.Equal("ta gamma delta", chunks[1].Content);
    }

    [Fact]
    public void Chunk_ShouldFallbackToHardCutWhenNoSeparatorExists()
    {
        var chunker = CreateChunker(chunkSize: 5, chunkOverlap: 1);

        var chunks = chunker.Chunk(Guid.NewGuid(), "abcdefghijk");

        Assert.Equal(["abcde", "efghi", "ijk"], chunks.Select(chunk => chunk.Content).ToArray());
    }

    [Fact]
    public void Chunk_ShouldBeDeterministicForSameInput()
    {
        var documentId = Guid.NewGuid();
        var chunker = CreateChunker(chunkSize: 12, chunkOverlap: 4);

        var first = chunker.Chunk(documentId, "alpha beta gamma delta epsilon");
        var second = chunker.Chunk(documentId, "alpha beta gamma delta epsilon");

        Assert.Equal(first.Count, second.Count);
        for (var index = 0; index < first.Count; index++)
        {
            Assert.Equal(first[index].Id, second[index].Id);
            Assert.Equal(first[index].Order, second[index].Order);
            Assert.Equal(first[index].Content, second[index].Content);
        }
    }

    [Fact]
    public void Chunk_ShouldRejectInvalidInput()
    {
        var chunker = CreateChunker();

        Assert.Throws<ArgumentException>(() => chunker.Chunk(Guid.Empty, "valid text"));
        Assert.Throws<ArgumentException>(() => chunker.Chunk(Guid.NewGuid(), "   "));
    }

    [Fact]
    public void Chunk_ShouldNormalizeNewlinesBeforeChunking()
    {
        var chunker = CreateChunker(chunkSize: 20, chunkOverlap: 0);

        var chunks = chunker.Chunk(Guid.NewGuid(), "line 1\r\nline 2\rline 3");

        var chunk = Assert.Single(chunks);
        Assert.Equal("line 1\nline 2\nline 3", chunk.Content);
    }

    private static IDocumentChunker CreateChunker(int chunkSize = 12, int chunkOverlap = 2)
    {
        return new SimpleDeterministicDocumentChunker(Options.Create(new IngestionOptions
        {
            ChunkSize = chunkSize,
            ChunkOverlap = chunkOverlap,
            MaxFileSizeMb = 25,
            AllowedExtensions = [".pdf"]
        }));
    }
}
