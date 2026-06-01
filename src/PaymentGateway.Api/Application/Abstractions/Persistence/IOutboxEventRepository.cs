using PaymentGateway.Api.Domain.Entities.Outbox;

namespace PaymentGateway.Api.Application.Abstractions.Persistence
{
    public interface IOutboxEventRepository
    {
        Task AddAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken);
    }
}
