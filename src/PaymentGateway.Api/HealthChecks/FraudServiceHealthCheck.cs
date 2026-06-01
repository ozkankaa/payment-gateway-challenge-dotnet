using Microsoft.Extensions.Diagnostics.HealthChecks;

using PaymentGateway.Api.Infrastructure.Services.FraudService;
using PaymentGateway.Api.Infrastructure.Services.FraudService.Requests;

namespace PaymentGateway.Api.HealthChecks;

public sealed class FraudServiceHealthCheck(
        IFraudServiceClient fraudServiceClient,
        ILogger<FraudServiceHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use a valid request that the simulator will accept and return 200.
            // Card number ends with '1' to force the simulator to return a 200 (unauthorized, but reachable).
            var probe = new FraudCheckRequest(CardNumber: "4111111111111111");

            var response = await fraudServiceClient.CheckAsync(probe, cancellationToken).ConfigureAwait(false);

            if (response is not null)
            {
                logger.LogDebug("Fraud service health check succeeded (response received).");
                return HealthCheckResult.Healthy("Fraud service is reachable.");
            }

            logger.LogWarning("Fraud service health check returned null response.");
            return HealthCheckResult.Degraded("Fraud service returned no content.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            logger.LogError(ex, "Fraud service returned 503 Service Unavailable.");
            return HealthCheckResult.Unhealthy("Fraud service is unavailable (503).", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fraud service health check failed with an unexpected error.");
            return HealthCheckResult.Unhealthy("Fraud service health check failed.", ex);
        }
    }
}