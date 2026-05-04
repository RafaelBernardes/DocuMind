using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Persistence;

public sealed class DocuMindDbContext(
    DbContextOptions<DocuMindDbContext> options,
    IOptions<PostgresOptions> postgresOptions) : DbContext(options)
{
    private readonly string _schema = postgresOptions.Value.Schema;

    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();

    public DbSet<DocumentChunkEntity> DocumentChunks => Set<DocumentChunkEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocuMindDbContext).Assembly);
    }
}
