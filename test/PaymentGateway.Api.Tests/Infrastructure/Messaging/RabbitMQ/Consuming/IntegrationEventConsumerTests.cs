using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.Application.Abstractions.CQRS;
using PaymentGateway.Api.Application.Features.Payments.IntegrationEvents;
using PaymentGateway.Api.Infrastructure.Messaging.RabbitMQ.Consuming;
using PaymentGateway.Api.Options;

using RabbitMQ.Client;

namespace PaymentGateway.Api.Tests.Infrastructure.Messaging.RabbitMQ.Consuming;

[Collection(nameof(RabbitMqTestCollection))]
public sealed class IntegrationEventConsumerTests(RabbitMqTestFixture fixture)
{
    [Fact]
    public async Task Consumer_WhenMessageIsPublished_HandlesMessageAndAcknowledgesIt()
    {
        // Arrange
        var exchangeName = $"payment-events-{Guid.NewGuid():N}";
        var queueName = $"payment-events-queue-{Guid.NewGuid():N}";
        var routingKey = "payment.authorised";

        var options = CreateOptions(exchangeName, queueName, routingKey);

        var spyHandler = new SpyIntegrationEventHandler();

        var services = new ServiceCollection()
         .AddSingleton<IIntegrationEventHandler>(spyHandler)
         .AddSingleton<ICommandHandler<IntegrationEvent>, SpyIntegrationEventHandler>()
         .AddSingleton<SpyIntegrationEventHandler>()
         .AddSingleton(spyHandler)
         .BuildServiceProvider();

        var consumer = new IntegrationEventConsumer(
            services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<IntegrationEventConsumer>.Instance);

        var messageId = Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();
        const string messageType = "PaymentAuthorisedIntegrationEvent";

        var body = """
        {
          "paymentId": "payment-123",
          "amount": 100,
          "currency": "GBP"
        }
        """;

        await consumer.StartAsync(CancellationToken.None);

        await WaitUntilAsync(
            async () => await QueueExistsAsync(queueName),
            TimeSpan.FromSeconds(10));

        // Act
        await PublishMessageAsync(
            exchangeName,
            routingKey,
            body,
            messageId,
            correlationId,
            messageType);

        var handledEvent = await spyHandler.WaitForMessageAsync(
            TimeSpan.FromSeconds(10));

        // Assert
        Assert.NotNull(handledEvent);
        Assert.Equal(messageType, handledEvent.MessageType);
        Assert.Equal(NormalizeJson(body), NormalizeJson(handledEvent.Content));

        var messageCount = await GetQueueMessageCountAsync(queueName);

        Assert.Equal(0u, messageCount);

        await consumer.StopAsync(CancellationToken.None);
        consumer.Dispose();
    }

    [Fact]
    public async Task Consumer_WhenHandlerThrows_NacksAndRequeuesMessage()
    {
        // Arrange
        var exchangeName = $"payment-events-{Guid.NewGuid():N}";
        var queueName = $"payment-events-queue-{Guid.NewGuid():N}";
        var routingKey = "payment.failed";

        var options = CreateOptions(exchangeName, queueName, routingKey);

        var failingHandler = new FailingIntegrationEventHandler();

        var services = new ServiceCollection()
            .AddSingleton<IIntegrationEventHandler>(failingHandler)
            .BuildServiceProvider();

        var consumer = new IntegrationEventConsumer(
            services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<IntegrationEventConsumer>.Instance);

        await consumer.StartAsync(CancellationToken.None);

        await WaitUntilAsync(
            async () => await QueueExistsAsync(queueName),
            TimeSpan.FromSeconds(10));

        await PublishMessageAsync(
            exchangeName,
            routingKey,
            """
        {
          "paymentId": "payment-999"
        }
        """,
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            "PaymentFailedIntegrationEvent");

        // Assert: handler was called and failed
        await failingHandler.WaitUntilCalledAsync(TimeSpan.FromSeconds(10));

        // Stop consumer so requeued message is no longer immediately consumed again
        await consumer.StopAsync(CancellationToken.None);
        consumer.Dispose();

        await WaitUntilAsync(
            async () => await GetQueueMessageCountAsync(queueName) >= 1,
            TimeSpan.FromSeconds(10));

        var messageCount = await GetQueueMessageCountAsync(queueName);

        Assert.Equal(1u, messageCount);
    }

    private RabbitMqOptions CreateOptions(
        string exchangeName,
        string queueName,
        string routingKey)
    {
        return new RabbitMqOptions
        {
            HostName = fixture.HostName,
            Port = fixture.Port,
            UserName = fixture.UserName,
            Password = fixture.PasswordValue,
            VirtualHost = fixture.VirtualHost,
            ExchangeName = exchangeName,
            QueueName = queueName,
            RoutingKey = routingKey,
            PrefetchCount = 1
        };
    }

    private async Task PublishMessageAsync(
        string exchangeName,
        string routingKey,
        string body,
        string messageId,
        string correlationId,
        string messageType)
    {
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = messageId,
            CorrelationId = correlationId,
            Type = messageType
        };

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(body));
    }

    private async Task<uint> GetQueueMessageCountAsync(string queueName)
    {
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var queue = await channel.QueueDeclarePassiveAsync(queueName);

        return queue.MessageCount;
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = fixture.HostName,
            Port = fixture.Port,
            UserName = fixture.UserName,
            Password = fixture.PasswordValue,
            VirtualHost = fixture.VirtualHost
        };

        return await factory.CreateConnectionAsync();
    }

    private static async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout)
    {
        var until = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < until)
        {
            if (await condition())
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException("Condition was not met before timeout.");
    }

    private async Task<bool> QueueExistsAsync(string queueName)
    {
        try
        {
            await using var connection = await CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclarePassiveAsync(queueName);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeJson(string json)
    {
        return json
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Replace(" ", string.Empty);
    }
}