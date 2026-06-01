using PaymentGateway.Api.Application.Abstractions.Persistence;
using PaymentGateway.Api.Application.Features.Payments.DomainEvents;
using PaymentGateway.Api.Domain.Abstractions;

namespace PaymentGateway.Api.Infrastructure.Persistence;

public sealed class UnitOfWork(PaymentDbContext dbContext, IDomainEventHandler domainEventHandler) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = dbContext
            .ChangeTracker
            .Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .SelectMany(aggregateRoot =>
            {
                var domainEvents = aggregateRoot.DomainEvents.ToArray();

                aggregateRoot.ClearDomainEvents();

                return domainEvents;
            })
            .ToArray();

        foreach (var domainEvent in domainEvents)
        {
            await domainEventHandler.HandleAsync(domainEvent, cancellationToken);
        }

        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}