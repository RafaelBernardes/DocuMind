using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Configuration;

public sealed class PostgresOptionsValidator : IValidateOptions<PostgresOptions>
{
    public ValidateOptionsResult Validate(string? name, PostgresOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("Postgres:ConnectionString is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Schema))
        {
            return ValidateOptionsResult.Fail("Postgres:Schema is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
