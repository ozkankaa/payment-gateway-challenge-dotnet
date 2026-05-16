using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Common;

public sealed class FakeAcquiringBankClient : IAcquiringBankClient
{
    public static int CallCount;

    public static void Reset() => Interlocked.Exchange(ref CallCount, 0);

    public async Task<BankPaymentResponse?> ProcessAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CallCount);
        await Task.Delay(25, cancellationToken);
        var last = request.CardNumber[^1];
        if (last == '0') throw new HttpRequestException("Bank unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);
        return new BankPaymentResponse(last is '1' or '3' or '5' or '7' or '9', Guid.NewGuid().ToString());
    }
}
