using DocuMind.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DocuMind.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options) : IRabbitMqConnectionFactory
{
    private readonly RabbitMqOptions _options = options.Value;

    public ConnectionFactory Create()
    {
        return new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };
    }

    public ValueTask<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask<IConnection>(Create().CreateConnectionAsync(cancellationToken));
    }
}
