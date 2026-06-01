using PaymentGateway.Api.Application.Features.Payments.DomainEvents.PaymentCaptured;
using PaymentGateway.Api.Domain.Abstractions;
using PaymentGateway.Api.Domain.Events.PaymentCaptured;

namespace PaymentGateway.Api.Application.Features.Payments.DomainEvents;

public class DomainEventHandler(PaymentCapturedDomainEventHandler paymentCapturedDomainEventHandler) 
    : IDomainEventHandler
{
    public async Task<bool> HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent is PaymentCapturedDomainEvent)
        {
            return await paymentCapturedDomainEventHandler.HandleAsync((PaymentCapturedDomainEvent)domainEvent, cancellationToken);
        }

        return false;
    }
}
