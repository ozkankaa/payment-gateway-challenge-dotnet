using PaymentGateway.Api.Domain.Entities.Outbox;

namespace PaymentGateway.Api.Infrastructure.Messaging.Abstraction;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(
        OutboxEvent message,
        CancellationToken cancellationToken = default);
}
