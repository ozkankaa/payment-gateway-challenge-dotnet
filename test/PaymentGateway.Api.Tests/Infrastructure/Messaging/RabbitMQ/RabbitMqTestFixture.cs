using Testcontainers.RabbitMq;

namespace PaymentGateway.Api.Tests.Infrastructure.Messaging.RabbitMQ;

public sealed class RabbitMqTestFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer =
        new RabbitMqBuilder("rabbitmq:3.13-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

    public string HostName => _rabbitMqContainer.Hostname;

    public int Port => _rabbitMqContainer.GetMappedPublicPort(5672);

    public string UserName => "guest";

    public string PasswordValue => "guest";

    public string VirtualHost => "/";

    public async ValueTask InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _rabbitMqContainer.DisposeAsync();
    }
}