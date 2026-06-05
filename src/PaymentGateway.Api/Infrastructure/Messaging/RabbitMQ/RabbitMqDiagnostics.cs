using System.Diagnostics;
using System.Text;

using PaymentGateway.Api.Domain.Entities.Outbox;

using RabbitMQ.Client;

namespace PaymentGateway.Api.Infrastructure.Messaging.RabbitMQ;

public static class RabbitMqDiagnostics
{
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";

    public static readonly ActivitySource ActivitySource =
        new("PaymentGateway.RabbitMQ");

    public static string GetOrCreateCorrelationId(OutboxEvent message)
    {
        return message.Id.ToString();
    }

    public static void InjectTraceContext(
        BasicProperties properties,
        Activity? activity)
    {
        if (activity is null)
            return;

        properties.Headers ??= new Dictionary<string, object?>();

        properties.Headers[TraceParentHeader] =
            Encoding.UTF8.GetBytes(activity.Id!);

        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
        {
            properties.Headers[TraceStateHeader] =
                Encoding.UTF8.GetBytes(activity.TraceStateString);
        }
    }

    public static string? ExtractHeaderAsString(
        IReadOnlyBasicProperties properties,
        string headerName)
    {
        return properties.Headers is null
            ? null
            : !properties.Headers.TryGetValue(headerName, out var value)
            ? null
            : value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => value!.ToString()
        };
    }
}