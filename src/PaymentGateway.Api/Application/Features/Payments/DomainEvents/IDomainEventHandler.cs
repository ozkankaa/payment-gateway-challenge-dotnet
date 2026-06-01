using PaymentGateway.Api.Domain.Abstractions;

namespace PaymentGateway.Api.Application.Features.Payments.DomainEvents;

public interface IDomainEventHandler
{
    Task<bool> HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}