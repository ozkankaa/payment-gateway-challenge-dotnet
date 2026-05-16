using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IAcquiringBankClient
{
    Task<BankPaymentResponse?> ProcessAsync(BankPaymentRequest request, CancellationToken cancellationToken);
}
