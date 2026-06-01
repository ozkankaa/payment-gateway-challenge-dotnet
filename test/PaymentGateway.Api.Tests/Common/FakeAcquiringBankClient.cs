using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService;
using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Requests;
using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Responses;

namespace PaymentGateway.Api.Tests.Common;

public sealed class FakeAcquiringBankClient : IAcquiringBankClient
{
    public static int CallCounts;

    public static void Reset() => Interlocked.Exchange(ref CallCounts, 0);

    public async Task<BankPaymentResponse?> ProcessAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CallCounts);
        await Task.Delay(25, cancellationToken);
        var last = request.CardNumber[^1];
        return last == '0'
            ? throw new HttpRequestException("Bank unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable)
            : new BankPaymentResponse(last is '1' or '3' or '5' or '7' or '9', Guid.NewGuid().ToString());
    }
}
