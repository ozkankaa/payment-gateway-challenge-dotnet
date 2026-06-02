using PaymentGateway.Api.Infrastructure.Services.FraudService.Requests;
using PaymentGateway.Api.Infrastructure.Services.FraudService.Responses;

namespace PaymentGateway.Api.Infrastructure.Services.FraudService;

public interface IFraudServiceClient
{
    Task<FraudCheckResponse?> CheckAsync(FraudCheckRequest checkRequest, CancellationToken cancellationToken = default);
}
