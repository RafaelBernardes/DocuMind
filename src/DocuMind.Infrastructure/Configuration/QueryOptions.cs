namespace DocuMind.Infrastructure.Configuration;

public sealed class QueryOptions
{
    public const string SectionName = "Query";

    public int TopK { get; set; } = 5;
    public double MinScore { get; set; } = 0.7d;
    public int MaxContextChunks { get; set; } = 8;
}
