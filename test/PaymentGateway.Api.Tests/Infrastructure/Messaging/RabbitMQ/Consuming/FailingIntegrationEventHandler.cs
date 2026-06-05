using PaymentGateway.Api.Application.Features.Payments.IntegrationEvents;

namespace PaymentGateway.Api.Tests.Infrastructure.Messaging.RabbitMQ.Consuming;

public sealed class FailingIntegrationEventHandler : IIntegrationEventHandler
{
    private readonly TaskCompletionSource _called =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitUntilCalledAsync(TimeSpan timeout)
    {
        return _called.Task.WaitAsync(timeout);
    }

    public Task HandleAsync(
        IntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        _called.TrySetResult();

        throw new InvalidOperationException("Test handler failure.");
    }
}