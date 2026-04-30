namespace DocuMind.Infrastructure.Configuration;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public int ChunkSize { get; set; } = 1200;
    public int ChunkOverlap { get; set; } = 200;
    public int MaxFileSizeMb { get; set; } = 25;
    public string[] AllowedExtensions { get; set; } = [];
}
