namespace DocuMind.Core.Documents;

public interface IDocumentChunker
{
    IReadOnlyList<Chunk> Chunk(Guid documentId, string text);
}
