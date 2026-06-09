using System.Reflection;
using System.Text;
using DocuMind.Core.Documents.IntegrationEvents;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Messaging.DocumentIngestion;
using DocuMind.Infrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DocuMind.Core.Tests.Messaging;

public sealed class DocumentIngestionConsumerHostedServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldNotNackWhenHandlerCancelsWithStoppingToken()
    {
        FakeConnectionProxy.Reset();
        FakeChannelProxy.Reset();

        var handler = new CancellingDocumentIngestionMessageHandler();
        var services = new ServiceCollection();
        services.AddScoped<IDocumentIngestionMessageHandler>(_ => handler);
        services.AddLogging();

        await using var provider = services.BuildServiceProvider();
        var connectionFactory = new FakeRabbitMqConnectionFactory();
        var service = new DocumentIngestionConsumerHostedService(
            connectionFactory,
            new FakeRabbitMqTopologyInitializer(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(CreateOptions()),
            NullLogger<DocumentIngestionConsumerHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await handler.HandledAsync;
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, FakeConnectionProxy.CreateChannelCalls);
        Assert.Equal(1, FakeChannelProxy.BasicConsumeCalls);
        Assert.Equal(0, FakeChannelProxy.BasicAckCalls);
        Assert.Equal(0, FakeChannelProxy.BasicNackCalls);
    }

    private static RabbitMqOptions CreateOptions()
    {
        return new RabbitMqOptions
        {
            Enabled = true,
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/",
            QueueName = "documind.document-ingestion",
            ExchangeName = "documind.documents",
            DeadLetterExchangeName = "documind.documents.dlx",
            DeadLetterQueueName = "documind.document-ingestion.dlq",
            DeadLetterRoutingKey = "documents.uploaded.dlq",
            RoutingKey = "documents.uploaded",
            PrefetchCount = 1
        };
    }

    private sealed class CancellingDocumentIngestionMessageHandler : IDocumentIngestionMessageHandler
    {
        private readonly TaskCompletionSource _handled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task HandledAsync => _handled.Task;

        public Task HandleAsync(DocumentUploadedMessage message, CancellationToken cancellationToken = default)
        {
            _handled.TrySetResult();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class FakeRabbitMqConnectionFactory : IRabbitMqConnectionFactory
    {
        public ValueTask<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IConnection>(DispatchProxy.Create<IConnection, FakeConnectionProxy>());
        }
    }

    private sealed class FakeRabbitMqTopologyInitializer : IRabbitMqTopologyInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeConnectionProxy : DispatchProxy
    {
        public static int CreateChannelCalls { get; private set; }

        public static void Reset()
        {
            CreateChannelCalls = 0;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                return null;
            }

            if (targetMethod.Name == nameof(IConnection.CreateChannelAsync))
            {
                CreateChannelCalls++;
                return Task.FromResult<IChannel>(DispatchProxy.Create<IChannel, FakeChannelProxy>());
            }

            if (targetMethod.Name == nameof(IAsyncDisposable.DisposeAsync))
            {
                return ValueTask.CompletedTask;
            }

            return DefaultValue(targetMethod.ReturnType);
        }
    }

    private class FakeChannelProxy : DispatchProxy
    {
        public static int BasicConsumeCalls { get; private set; }
        public static int BasicAckCalls { get; private set; }
        public static int BasicNackCalls { get; private set; }

        public static void Reset()
        {
            BasicConsumeCalls = 0;
            BasicAckCalls = 0;
            BasicNackCalls = 0;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                return null;
            }

            switch (targetMethod.Name)
            {
                case nameof(IChannel.ExchangeDeclareAsync):
                case nameof(IChannel.QueueDeclareAsync):
                case nameof(IChannel.QueueBindAsync):
                case nameof(IChannel.BasicQosAsync):
                    return Task.CompletedTask;
                case nameof(IChannel.BasicAckAsync):
                    BasicAckCalls++;
                    return Task.CompletedTask;
                case nameof(IChannel.BasicNackAsync):
                    BasicNackCalls++;
                    return Task.CompletedTask;
                case nameof(IChannel.BasicConsumeAsync):
                    BasicConsumeCalls++;
                    var consumer = Assert.IsType<AsyncEventingBasicConsumer>(args![6]);
                    consumer.HandleBasicDeliverAsync(
                        "",
                        42UL,
                        false,
                        "exchange",
                        "documents.uploaded",
                        null!,
                        Encoding.UTF8.GetBytes("""{"documentId":"11111111-1111-1111-1111-111111111111","fileName":"contract.pdf","contentType":"application/pdf","sizeInBytes":1024,"storageRelativePath":"uploads/contract.pdf","uploadedAtUtc":"2026-06-08T00:00:00+00:00"}"""),
                        CancellationToken.None).GetAwaiter().GetResult();
                    return Task.FromResult("consumer-tag");
                case nameof(IChannel.BasicCancelAsync):
                    return Task.CompletedTask;
                case nameof(IAsyncDisposable.DisposeAsync):
                    return ValueTask.CompletedTask;
                default:
                    return DefaultValue(targetMethod.ReturnType);
            }
        }
    }

    private static object? DefaultValue(Type returnType)
    {
        if (returnType == typeof(void) || returnType == typeof(ValueTask))
        {
            return ValueTask.CompletedTask;
        }

        if (returnType == typeof(ValueTask<string>))
        {
            return ValueTask.FromResult(string.Empty);
        }

        if (returnType.IsValueType)
        {
            return Activator.CreateInstance(returnType);
        }

        return null;
    }
}
