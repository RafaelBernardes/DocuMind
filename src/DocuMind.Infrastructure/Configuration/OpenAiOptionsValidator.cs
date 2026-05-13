using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Configuration;

public sealed class OpenAiOptionsValidator : IValidateOptions<OpenAiOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenAiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return ValidateOptionsResult.Fail("OpenAI:Endpoint is required.");
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("OpenAI:Endpoint must be a valid absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail(
                $"OpenAI API key is required via environment variable '{OpenAiOptions.ApiKeyEnvironmentVariableName}'.");
        }

        if (string.IsNullOrWhiteSpace(options.ChatModel))
        {
            return ValidateOptionsResult.Fail("OpenAI:ChatModel is required.");
        }

        if (string.IsNullOrWhiteSpace(options.EmbeddingModel))
        {
            return ValidateOptionsResult.Fail("OpenAI:EmbeddingModel is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
