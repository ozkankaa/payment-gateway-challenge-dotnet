using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Requests;
using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Responses;

namespace PaymentGateway.Api.Infrastructure.Services.AcquiringBankService;

public class AcquiringBankClient(HttpClient httpClient, ILogger<AcquiringBankClient> logger) : IAcquiringBankClient
{
    public async Task<BankPaymentResponse?> ProcessAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("/payments", request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            logger.LogWarning("Acquiring bank is unavailable for masked card ending {last_four_card_digits}", request.CardNumber[^4..]);
            throw new HttpRequestException("Acquiring bank is unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            logger.LogWarning("Acquiring bank returned a bad request for masked card ending {last_four_card_digits}", request.CardNumber[^4..]);
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken: cancellationToken);
    }
}
