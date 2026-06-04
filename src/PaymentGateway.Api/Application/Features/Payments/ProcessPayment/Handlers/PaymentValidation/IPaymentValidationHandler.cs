using PaymentGateway.Api.Application.Abstractions.CQRS;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.PaymentValidation;

public interface IPaymentValidationHandler: ICommandHandler<ProcessPaymentCommand, IDictionary<string, string[]>>
{
}
