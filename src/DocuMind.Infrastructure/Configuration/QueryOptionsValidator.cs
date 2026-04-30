using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Configuration;

public sealed class QueryOptionsValidator : IValidateOptions<QueryOptions>
{
    public ValidateOptionsResult Validate(string? name, QueryOptions options)
    {
        if (options.TopK <= 0)
        {
            return ValidateOptionsResult.Fail("Query:TopK must be greater than zero.");
        }

        if (options.MinScore is < 0 or > 1)
        {
            return ValidateOptionsResult.Fail("Query:MinScore must be between 0 and 1.");
        }

        if (options.MaxContextChunks <= 0)
        {
            return ValidateOptionsResult.Fail("Query:MaxContextChunks must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
