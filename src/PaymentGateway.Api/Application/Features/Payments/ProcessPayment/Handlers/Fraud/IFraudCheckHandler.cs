using PaymentGateway.Api.Application.Abstractions.CQRS;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Fraud;

public interface IFraudCheckHandler : ICommandHandler<FraudCheckCommand, FraudCheckResult>
{
}
