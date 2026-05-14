using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Messaging.Outbox;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocuMind.Core.Tests.Messaging;

public sealed class OutboxPublisherServiceTests
{
    [Fact]
    public async Task PublishPendingMessagesAsync_ShouldMarkMessageAsProcessedOnSuccess()
    {
        await using var dbContext = CreateDbContext();
        var message = CreateOutboxMessage();
        await dbContext.OutboxMessages.AddAsync(message);
        await dbContext.SaveChangesAsync();

        var publisher = new FakeOutboxMessagePublisher();
        var service = CreateService(dbContext, publisher, enabled: true);

        var publishedCount = await service.PublishPendingMessagesAsync();

        var persistedMessage = await dbContext.OutboxMessages.SingleAsync();
        Assert.Equal(1, publishedCount);
        Assert.Single(publisher.PublishedMessageIds, message.Id);
        Assert.NotNull(persistedMessage.ProcessedAtUtc);
        Assert.Null(persistedMessage.Error);
    }

    [Fact]
    public async Task PublishPendingMessagesAsync_ShouldLeaveMessagePendingAndCaptureErrorOnFailure()
    {
        await using var dbContext = CreateDbContext();
        var message = CreateOutboxMessage();
        await dbContext.OutboxMessages.AddAsync(message);
        await dbContext.SaveChangesAsync();

        var publisher = new FakeOutboxMessagePublisher { ExceptionToThrow = new InvalidOperationException("broker down") };
        var service = CreateService(dbContext, publisher, enabled: true);

        var publishedCount = await service.PublishPendingMessagesAsync();

        var persistedMessage = await dbContext.OutboxMessages.SingleAsync();
        Assert.Equal(0, publishedCount);
        Assert.Null(persistedMessage.ProcessedAtUtc);
        Assert.Equal("broker down", persistedMessage.Error);
    }

    [Fact]
    public async Task PublishPendingMessagesAsync_ShouldNoOpWhenRabbitMqIsDisabled()
    {
        await using var dbContext = CreateDbContext();
        var message = CreateOutboxMessage();
        await dbContext.OutboxMessages.AddAsync(message);
        await dbContext.SaveChangesAsync();

        var publisher = new FakeOutboxMessagePublisher();
        var service = CreateService(dbContext, publisher, enabled: false);

        var publishedCount = await service.PublishPendingMessagesAsync();

        Assert.Equal(0, publishedCount);
        Assert.Empty(publisher.PublishedMessageIds);
    }

    private static OutboxPublisherService CreateService(
        DocuMindDbContext dbContext,
        IOutboxMessagePublisher publisher,
        bool enabled)
    {
        return new OutboxPublisherService(
            dbContext,
            publisher,
            Options.Create(new RabbitMqOptions
            {
                Enabled = enabled,
                PublishBatchSize = 20,
                PollIntervalSeconds = 2
            }),
            NullLogger<OutboxPublisherService>.Instance);
    }

    private static OutboxMessageEntity CreateOutboxMessage()
    {
        return new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = "documents.uploaded",
            DocumentId = Guid.NewGuid(),
            Payload = """{"documentId":"11111111-1111-1111-1111-111111111111"}""",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static DocuMindDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DocuMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new TestDocuMindDbContext(
            options,
            Options.Create(new PostgresOptions
            {
                ConnectionString = "Host=localhost;Database=documind;Username=postgres;Password=postgres",
                Schema = "public"
            }));
    }

    private sealed class FakeOutboxMessagePublisher : IOutboxMessagePublisher
    {
        public Exception? ExceptionToThrow { get; init; }
        public List<Guid> PublishedMessageIds { get; } = [];

        public Task PublishAsync(OutboxMessageEntity message, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            PublishedMessageIds.Add(message.Id);
            return Task.CompletedTask;
        }
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
