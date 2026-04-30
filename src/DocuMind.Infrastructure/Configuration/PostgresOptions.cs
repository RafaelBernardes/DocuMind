namespace DocuMind.Infrastructure.Configuration;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    public string ConnectionString { get; set; } = string.Empty;
    public string Schema { get; set; } = "public";
}
