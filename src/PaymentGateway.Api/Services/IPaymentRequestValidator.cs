using PaymentGateway.Api.Application.Payments.ProcessPayment;

namespace PaymentGateway.Api.Services;

public interface IPaymentRequestValidator
{
    IDictionary<string, string[]> Validate(ProcessPaymentCommand request);
}
