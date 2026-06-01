using PaymentGateway.Api.Application.Abstractions.CQRS;

namespace PaymentGateway.Api.Application.Features.Payments.IntegrationEvents;

public interface IIntegrationEventHandler : ICommandHandler<IntegrationEvent>
{
}
