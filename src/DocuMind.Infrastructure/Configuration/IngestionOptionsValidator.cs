using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Configuration;

public sealed class IngestionOptionsValidator : IValidateOptions<IngestionOptions>
{
    public ValidateOptionsResult Validate(string? name, IngestionOptions options)
    {
        if (options.ChunkSize <= 0)
        {
            return ValidateOptionsResult.Fail("Ingestion:ChunkSize must be greater than zero.");
        }

        if (options.ChunkOverlap < 0)
        {
            return ValidateOptionsResult.Fail("Ingestion:ChunkOverlap cannot be negative.");
        }

        if (options.ChunkOverlap >= options.ChunkSize)
        {
            return ValidateOptionsResult.Fail("Ingestion:ChunkOverlap must be smaller than Ingestion:ChunkSize.");
        }

        if (options.MaxFileSizeMb <= 0)
        {
            return ValidateOptionsResult.Fail("Ingestion:MaxFileSizeMb must be greater than zero.");
        }

        if (options.AllowedExtensions is null || options.AllowedExtensions.Length == 0)
        {
            return ValidateOptionsResult.Fail("Ingestion:AllowedExtensions must contain at least one extension.");
        }

        return ValidateOptionsResult.Success;
    }
}
