using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Domain.Entities.Outbox;
using PaymentGateway.Api.Infrastructure.Messaging.Abstraction;
using PaymentGateway.Api.Options;

using RabbitMQ.Client;

namespace PaymentGateway.Api.Infrastructure.Messaging.RabbitMQ.Publishing;

public sealed class IntegrationEventPublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<IntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    private readonly RabbitMqOptions _options = options.Value;

    private static readonly ActivitySource _activitySource =
    new("PaymentGateway.Api");

    public async Task PublishAsync(
        OutboxEvent message,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity(
            "Process Integration Event",
            ActivityKind.Producer);

        var correlationId = RabbitMqDiagnostics.GetOrCreateCorrelationId(message);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", _options.ExchangeName);
        activity?.SetTag("messaging.rabbitmq.routing_key", _options.RoutingKey);
        activity?.SetTag("messaging.message.id", message.Id.ToString());
        activity?.SetTag("messaging.message.conversation_id", correlationId);
        activity?.SetTag("messaging.message.type", message.Type);

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        await using var connection =
            await factory.CreateConnectionAsync(cancellationToken);

        await using var channel =
            await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _options.QueueName,
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey,
            cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.Id.ToString(),
            CorrelationId = correlationId,
            Type = message.Type,
            Timestamp = new AmqpTimestamp(
                new DateTimeOffset(message.OccurredAtUtc).ToUnixTimeSeconds())
        };

        RabbitMqDiagnostics.InjectTraceContext(properties, activity);

        var body = Encoding.UTF8.GetBytes(message.Content);

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = GetTraceId(activity),
            ["SpanId"] = activity?.SpanId.ToString(),
            ["CorrelationId"] = correlationId,
            ["OutboxMessageId"] = message.Id,
            ["MessageType"] = message.Type
        }))
        {
            logger.LogInformation(
                "Publishing RabbitMQ message {MessageId} with correlation id {CorrelationId}.",
                message.Id,
                correlationId);

            await channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: _options.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Published RabbitMQ message {MessageId}.",
                message.Id);
        }
    }

    private static string? GetTraceId(Activity? activity)
    {
        return activity?.TraceId.ToString()
            ?? Activity.Current?.TraceId.ToString();
    }
}