using PaymentGateway.Api.Infrastructure.Services.FraudService;
using PaymentGateway.Api.Infrastructure.Services.FraudService.Requests;
using PaymentGateway.Api.Infrastructure.Services.FraudService.Responses;

namespace PaymentGateway.Api.Tests.Common;

public sealed class FakeFraudServiceClient : IFraudServiceClient
{
    private static int s_callCounts;

    public static int CallCounts => s_callCounts;

    public static void Reset() => Interlocked.Exchange(ref s_callCounts, 0);

    public async Task<FraudCheckResponse?> CheckAsync(FraudCheckRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref s_callCounts);
        await Task.Delay(25, cancellationToken);
        var last = request.CardNumber[^1];
        return last == '9'
            ? throw new HttpRequestException("Fraud service unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable)
            : new FraudCheckResponse(last is '2' or '3' or '4' or '5' or '6' or '7' or '8' or '0', Guid.NewGuid().ToString());
    }
}
