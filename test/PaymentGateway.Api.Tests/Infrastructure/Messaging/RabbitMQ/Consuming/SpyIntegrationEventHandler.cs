using PaymentGateway.Api.Application.Features.Payments.IntegrationEvents;

namespace PaymentGateway.Api.Tests.Infrastructure.Messaging.RabbitMQ.Consuming;

public sealed class SpyIntegrationEventHandler : IIntegrationEventHandler
{
    private readonly TaskCompletionSource<IntegrationEvent> _handledEvent =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IntegrationEvent? ReceivedEvent { get; private set; }

    public Task<IntegrationEvent> WaitForMessageAsync(
        TimeSpan timeout)
    {
        return _handledEvent.Task.WaitAsync(timeout);
    }

    public Task HandleAsync(
        IntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        ReceivedEvent = integrationEvent;
        _handledEvent.TrySetResult(integrationEvent);

        return Task.CompletedTask;
    }
}