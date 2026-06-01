
using PaymentGateway.Api.Application.Abstractions.Persistence;
using PaymentGateway.Api.Domain.Entities.Outbox;

namespace PaymentGateway.Api.Infrastructure.Persistence.Repositories;

public class OutboxEventRepository(PaymentDbContext context) : IOutboxEventRepository
{
    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken)
    {
        await context.OutboxEvents.AddAsync(outboxEvent, cancellationToken);
    }
}
