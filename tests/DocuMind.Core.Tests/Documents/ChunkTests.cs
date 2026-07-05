using DocuMind.Core.Documents;

namespace DocuMind.Core.Tests.Documents;

public class ChunkTests
{
    [Fact]
    public void Constructor_ShouldAllowNullEmbedding()
    {
        var chunk = CreateChunk(embedding: null);

        Assert.Null(chunk.Embedding);
    }

    [Fact]
    public void Constructor_ShouldStoreValidEmbedding()
    {
        var embedding = Enumerable
            .Range(0, EmbeddingConstants.ExpectedDimensions)
            .Select(index => (float)index)
            .ToArray();

        var chunk = CreateChunk(embedding);

        Assert.NotNull(chunk.Embedding);
        Assert.Equal(EmbeddingConstants.ExpectedDimensions, chunk.Embedding!.Count);
        Assert.Equal(embedding, chunk.Embedding);
    }

    [Fact]
    public void Constructor_ShouldRejectInvalidEmbeddingDimension()
    {
        var invalidEmbedding = Enumerable
            .Range(0, EmbeddingConstants.ExpectedDimensions - 1)
            .Select(index => (float)index)
            .ToArray();

        var exception = Assert.Throws<ArgumentException>(() => CreateChunk(invalidEmbedding));

        Assert.Equal("embedding", exception.ParamName);
        Assert.Contains(EmbeddingConstants.ExpectedDimensions.ToString(), exception.Message);
    }

    [Fact]
    public void Constructor_ShouldDefensivelyCopyEmbedding()
    {
        var embedding = Enumerable
            .Range(0, EmbeddingConstants.ExpectedDimensions)
            .Select(index => (float)index)
            .ToArray();

        var chunk = CreateChunk(embedding);

        embedding[0] = -1;

        Assert.NotNull(chunk.Embedding);
        Assert.Equal(0f, chunk.Embedding![0]);
    }

    private static Chunk CreateChunk(IReadOnlyList<float>? embedding)
    {
        return new Chunk(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "chunk body",
            new ChunkMetadata(characterCount: 10, tokenCount: 3, pageLabel: "1"),
            embedding);
    }
}
