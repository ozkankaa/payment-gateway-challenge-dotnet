using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Requests;
using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Responses;

namespace PaymentGateway.Api.Infrastructure.Services.AcquiringBankService;

public interface IAcquiringBankClient
{
    Task<BankPaymentResponse?> ProcessAsync(BankPaymentRequest request, CancellationToken cancellationToken);
}
