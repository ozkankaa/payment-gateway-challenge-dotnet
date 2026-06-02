using PaymentGateway.Api.Infrastructure.Services.FraudService.Requests;
using PaymentGateway.Api.Infrastructure.Services.FraudService.Responses;

namespace PaymentGateway.Api.Infrastructure.Services.FraudService;

public class FraudServiceClient(HttpClient httpClient, ILogger<FraudServiceClient> logger) : IFraudServiceClient
{
    public async Task<FraudCheckResponse?> CheckAsync(FraudCheckRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/frauds", request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            logger.LogWarning("Fraud service is unavailable for masked card ending {last_four_card_digits}", request.CardNumber[^4..]);
            throw new HttpRequestException("Fraud service is unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            logger.LogWarning("Fraud service returned a bad request for masked card ending {last_four_card_digits}", request.CardNumber[^4..]);
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FraudCheckResponse>(cancellationToken: cancellationToken);
    }
}
