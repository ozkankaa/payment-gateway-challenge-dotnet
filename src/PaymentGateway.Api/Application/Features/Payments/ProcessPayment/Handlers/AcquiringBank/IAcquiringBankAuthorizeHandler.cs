using PaymentGateway.Api.Application.Abstractions.CQRS;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.AcquiringBank;

public interface IAcquiringBankAuthorizeHandler : ICommandHandler<AcquiringBankAuthorizeCommand, AcquiringBankAuthorizeResult>
{
}
