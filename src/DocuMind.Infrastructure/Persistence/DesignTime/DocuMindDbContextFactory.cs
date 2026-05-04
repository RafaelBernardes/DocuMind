using DocuMind.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

namespace DocuMind.Infrastructure.Persistence.DesignTime;

public sealed class DocuMindDbContextFactory : IDesignTimeDbContextFactory<DocuMindDbContext>
{
    public DocuMindDbContext CreateDbContext(string[] args)
    {
        var configurationBasePath = ResolveApiConfigurationBasePath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(configurationBasePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration[$"{PostgresOptions.SectionName}:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Postgres:ConnectionString is required to create DocuMindDbContext at design time.");
        }

        var postgresOptions = new PostgresOptions
        {
            ConnectionString = connectionString,
            Schema = configuration[$"{PostgresOptions.SectionName}:Schema"] ?? "public"
        };

        if (!string.Equals(postgresOptions.Schema, "public", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Postgres:Schema currently supports only 'public' in the MVP.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<DocuMindDbContext>();
        optionsBuilder.UseNpgsql(
            postgresOptions.ConnectionString,
            npgsqlOptions => npgsqlOptions.UseVector());

        return new DocuMindDbContext(optionsBuilder.Options, Options.Create(postgresOptions));
    }

    private static string ResolveApiConfigurationBasePath()
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "DocuMind.sln");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(currentDirectory.FullName, "src", "DocuMind.Api");
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException(
            "Could not resolve the repository root to load appsettings from src/DocuMind.Api.");
    }
}
