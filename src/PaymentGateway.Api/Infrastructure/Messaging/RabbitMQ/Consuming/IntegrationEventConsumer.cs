using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Application.Features.Payments.IntegrationEvents;
using PaymentGateway.Api.Infrastructure.Messaging.Abstraction;
using PaymentGateway.Api.Options;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaymentGateway.Api.Infrastructure.Messaging.RabbitMQ.Consuming;

public sealed class IntegrationEventConsumer(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<IntegrationEventConsumer> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;

    private IConnection? _connection;
    private IChannel? _channel;

    private static readonly ActivitySource _activitySource = new("PaymentGateway.Api");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectAsync(stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var content = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
            var messageType = eventArgs.BasicProperties.Type ?? "unknown";

            var traceParent = RabbitMqDiagnostics.ExtractHeaderAsString(
                eventArgs.BasicProperties,
                RabbitMqDiagnostics.TraceParentHeader);

            var correlationId =
                eventArgs.BasicProperties.CorrelationId
                ?? eventArgs.BasicProperties.MessageId
                ?? Guid.NewGuid().ToString();

            using var activity = _activitySource.StartActivity(
            "Process Integration Event",
            ActivityKind.Consumer,
            parentId:traceParent);

            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination.name", _options.QueueName);
            activity?.SetTag("messaging.operation", "receive");
            activity?.SetTag("messaging.message.id", eventArgs.BasicProperties.MessageId);
            activity?.SetTag("messaging.message.conversation_id", correlationId);
            activity?.SetTag("messaging.message.type", messageType);

            using (logger.BeginScope(new Dictionary<string, object?>
            {
                ["TraceId"] = GetTraceId(activity),
                ["SpanId"] = activity?.SpanId.ToString(),
                ["CorrelationId"] = correlationId,
                ["MessageId"] = eventArgs.BasicProperties.MessageId,
                ["MessageType"] = messageType,
                ["DeliveryTag"] = eventArgs.DeliveryTag
            }))
            {
                try
                {
                    await using var scope = serviceScopeFactory.CreateAsyncScope();

                    var handler =
                        scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler>();

                    logger.LogInformation(
                        "Consuming RabbitMQ message {MessageId} with correlation id {CorrelationId}.",
                        eventArgs.BasicProperties.MessageId,
                        correlationId);

                    await handler.HandleAsync(new IntegrationEvent(messageType, content),
                        cancellationToken: stoppingToken);

                    await _channel!.BasicAckAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);

                    logger.LogInformation(
                        "RabbitMQ message {MessageId} consumed successfully.",
                        eventArgs.BasicProperties.MessageId);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to consume RabbitMQ message {MessageId}.",
                        eventArgs.BasicProperties.MessageId);

                    await _channel!.BasicNackAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        requeue: true,
                        cancellationToken: stoppingToken);
                }
            }
        };

        await _channel!.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);

        _channel = await _connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            queue: _options.QueueName,
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey,
            cancellationToken: cancellationToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _options.PrefetchCount,
            global: false,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "RabbitMQ consumer connected. Queue: {QueueName}, Exchange: {ExchangeName}, RoutingKey: {RoutingKey}",
            _options.QueueName,
            _options.ExchangeName,
            _options.RoutingKey);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);

        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }

    private static string? GetTraceId(Activity? activity)
    {
        return activity?.TraceId.ToString()
            ?? Activity.Current?.TraceId.ToString();
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();

        base.Dispose();
    }
}