using DocuMind.Core.Documents;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Messaging;
using DocuMind.Infrastructure.Messaging.DocumentIngestion;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocuMind.Core.Tests.Messaging;

public sealed class MessagingRegistrationTests
{
    [Fact]
    public void MessagingServicesAreRegisteredWithScopedIngestionBoundary()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<RabbitMqOptions>(_ => { });
        services.Configure<PostgresOptions>(options =>
        {
            options.ConnectionString = "Host=localhost;Database=documind;Username=postgres;Password=postgres";
            options.Schema = "public";
        });
        var dbContextOptions = new DbContextOptionsBuilder<DocuMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        services.AddSingleton(dbContextOptions);
        services.AddScoped<DocuMindDbContext>(serviceProvider =>
            new TestDocuMindDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<DocuMindDbContext>>(),
                serviceProvider.GetRequiredService<IOptions<PostgresOptions>>()));
        services.AddDocuMindMessaging();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IDocumentIngestionPipeline>();
        var handler = scope.ServiceProvider.GetRequiredService<IDocumentIngestionMessageHandler>();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

        Assert.NotNull(pipeline);
        Assert.NotNull(handler);
        Assert.NotNull(loggerFactory);
    }

    private sealed class TestDocuMindDbContext(
        DbContextOptions<DocuMindDbContext> options,
        IOptions<PostgresOptions> postgresOptions) : DocuMindDbContext(options, postgresOptions)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<DocumentChunkEntity>();
        }
    }
}
