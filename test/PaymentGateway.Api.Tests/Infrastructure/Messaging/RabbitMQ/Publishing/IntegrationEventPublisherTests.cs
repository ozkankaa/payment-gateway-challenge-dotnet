using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.Domain.Entities.Outbox;
using PaymentGateway.Api.Infrastructure.Messaging.RabbitMQ.Publishing;
using PaymentGateway.Api.Options;
using PaymentGateway.Api.Tests.Infrastructure.Messaging.RabbitMQ;
using PaymentGateway.Api.Tests.Integration.Messaging.RabbitMQ;

using RabbitMQ.Client;

namespace PaymentGateway.Api.Tests.Integration.Messaging.RabbitMQ.Publishing;

[Collection(nameof(RabbitMqTestCollection))]
public sealed class IntegrationEventPublisherTests
{
    private readonly RabbitMqTestFixture _fixture;

    public IntegrationEventPublisherTests(RabbitMqTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PublishAsync_WhenMessageIsValid_PublishesMessageToRabbitMq()
    {
        // Arrange
        var exchangeName = $"payment-events-{Guid.NewGuid():N}";
        var queueName = $"payment-events-queue-{Guid.NewGuid():N}";
        var routingKey = "payment.authorised";

        var options = Microsoft.Extensions.Options.Options.Create(new RabbitMqOptions
        {
            HostName = _fixture.HostName,
            Port = _fixture.Port,
            UserName = _fixture.UserName,
            Password = _fixture.PasswordValue,
            VirtualHost = _fixture.VirtualHost,
            ExchangeName = exchangeName,
            QueueName = queueName,
            RoutingKey = routingKey
        });

        var publisher = new IntegrationEventPublisher(
            options,
            NullLogger<IntegrationEventPublisher>.Instance);

        var outboxEvent = CreateOutboxEvent(
            type: "PaymentAuthorisedIntegrationEvent",
            content: """
            {
              "paymentId": "payment-123",
              "amount": 100,
              "currency": "GBP"
            }
            """);

        // Act
        await publisher.PublishAsync(outboxEvent);

        // Assert
        var publishedMessage = await ReadMessageFromQueueAsync(
            queueName,
            exchangeName,
            routingKey);

        Assert.NotNull(publishedMessage);

        Assert.Equal(
            NormalizeJson(outboxEvent.Content),
            NormalizeJson(publishedMessage.Body));

        Assert.Equal("application/json", publishedMessage.Properties.ContentType);
        Assert.Equal(outboxEvent.Id.ToString(), publishedMessage.Properties.MessageId);
        Assert.Equal(outboxEvent.Type, publishedMessage.Properties.Type);
        Assert.True(publishedMessage.Properties.Persistent);

        Assert.False(string.IsNullOrWhiteSpace(
            publishedMessage.Properties.CorrelationId));

        Assert.Equal(
            new DateTimeOffset(outboxEvent.OccurredAtUtc).ToUnixTimeSeconds(),
            publishedMessage.Properties.Timestamp.UnixTime);
    }

    [Fact]
    public async Task PublishAsync_WhenCalled_DeclaresDurableExchangeQueueAndBinding()
    {
        // Arrange
        var exchangeName = $"payment-events-{Guid.NewGuid():N}";
        var queueName = $"payment-events-queue-{Guid.NewGuid():N}";
        var routingKey = "payment.captured";

        var options = Microsoft.Extensions.Options.Options.Create(new RabbitMqOptions
        {
            HostName = _fixture.HostName,
            Port = _fixture.Port,
            UserName = _fixture.UserName,
            Password = _fixture.PasswordValue,
            VirtualHost = _fixture.VirtualHost,
            ExchangeName = exchangeName,
            QueueName = queueName,
            RoutingKey = routingKey
        });

        var publisher = new IntegrationEventPublisher(
            options,
            NullLogger<IntegrationEventPublisher>.Instance);

        var outboxEvent = CreateOutboxEvent(
            type: "PaymentCapturedIntegrationEvent",
            content: """
            {
              "paymentId": "payment-456"
            }
            """);

        // Act
        await publisher.PublishAsync(outboxEvent);

        // Assert
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var queue = await channel.QueueDeclarePassiveAsync(queueName);

        Assert.Equal(queueName, queue.QueueName);
        Assert.Equal(1u, queue.MessageCount);
        Assert.Equal(0u, queue.ConsumerCount);
    }

    private async Task<PublishedRabbitMqMessage> ReadMessageFromQueueAsync(
        string queueName,
        string exchangeName,
        string routingKey)
    {
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var result = await channel.BasicGetAsync(
                queue: queueName,
                autoAck: true);

            if (result is not null)
            {
                return new PublishedRabbitMqMessage(
                    Encoding.UTF8.GetString(result.Body.ToArray()),
                    result.BasicProperties);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(
            $"Message was not published to queue '{queueName}'.");
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = _fixture.HostName,
            Port = _fixture.Port,
            UserName = _fixture.UserName,
            Password = _fixture.PasswordValue,
            VirtualHost = _fixture.VirtualHost
        };

        return await factory.CreateConnectionAsync();
    }

    private static OutboxEvent CreateOutboxEvent(
        string type,
        string content)
    {
        return OutboxEvent.Create
        (
            type : type,
            content : content
        );
    }

    private static string NormalizeJson(string json)
    {
        return json
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Replace(" ", string.Empty);
    }

    private sealed record PublishedRabbitMqMessage(
        string Body,
        IReadOnlyBasicProperties Properties);
}