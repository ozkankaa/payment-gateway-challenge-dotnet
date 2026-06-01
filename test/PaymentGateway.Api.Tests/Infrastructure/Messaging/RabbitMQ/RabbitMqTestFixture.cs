using Testcontainers.RabbitMq;

namespace PaymentGateway.Api.Tests.Integration.Messaging.RabbitMQ;

public sealed class RabbitMqTestFixture : IAsyncLifetime
{
    private const string Username = "guest";
    private const string Password = "guest";

    private readonly RabbitMqContainer _rabbitMqContainer =
    new RabbitMqBuilder("rabbitmq:3.13-management")
        .WithUsername(Username)
        .WithPassword(Password)
        .Build();

    public string HostName => _rabbitMqContainer.Hostname;

    public int Port => _rabbitMqContainer.GetMappedPublicPort(5672);

    public string UserName => Username;

    public string PasswordValue => Password;

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