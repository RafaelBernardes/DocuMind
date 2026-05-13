namespace DocuMind.Infrastructure.Configuration;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";
    public const string ApiKeyEnvironmentVariableName = "DOCUMIND_OPENAI_API_KEY";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
}
