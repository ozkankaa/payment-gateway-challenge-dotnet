namespace PaymentGateway.Api.Options;

public sealed class RabbitMqOptions
{
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
    public string ExchangeName { get; init; } = "payment-integration-events";
    public string QueueName { get; init; } = "payment-integration-events-queue";
    public string RoutingKey { get; init; } = "payment.integration-event";
    public ushort PrefetchCount { get; init; } = 10;
}
