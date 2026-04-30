namespace DocuMind.Infrastructure.Configuration;

public sealed class LocalStorageOptions
{
    public const string SectionName = "LocalStorage";

    public string BasePath { get; set; } = string.Empty;
    public string UploadsPath { get; set; } = string.Empty;
    public string ProcessedPath { get; set; } = string.Empty;
}
