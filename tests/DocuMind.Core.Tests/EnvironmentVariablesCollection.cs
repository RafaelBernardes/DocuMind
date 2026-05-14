using Xunit;

namespace DocuMind.Core.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariablesCollection
{
    public const string Name = "Environment variables";
}
