namespace DocuMind.Core.Documents;

public enum LastProcessingStage
{
    None = 0,
    Claimed = 1,
    FileOpened = 2,
    TextExtracted = 3,
    ChunksCreated = 4,
    EmbeddingsGenerated = 5,
    IndexedPersisted = 6
}
