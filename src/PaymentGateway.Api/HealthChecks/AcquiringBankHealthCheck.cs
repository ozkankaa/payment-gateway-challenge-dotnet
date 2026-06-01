using Microsoft.Extensions.Diagnostics.HealthChecks;

using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService;
using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Requests;

namespace PaymentGateway.Api.HealthChecks;

public sealed class AcquiringBankHealthCheck(
        IAcquiringBankClient acquiringBankClient,
        ILogger<AcquiringBankHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use a valid request that the simulator will accept and return 200.
            // Card number ends with '2' to force the simulator to return a 200 (unauthorized, but reachable).
            var probe = new BankPaymentRequest(
                Amount: 1,
                Currency: "USD",
                CardNumber: "4111111111111112",
                ExpiryDate: "12/30",
                Cvv: "123");

            var response = await acquiringBankClient.ProcessAsync(probe, cancellationToken).ConfigureAwait(false);

            if (response is not null)
            {
                logger.LogDebug("Acquiring bank health check succeeded (response received).");
                return HealthCheckResult.Healthy("Acquiring bank is reachable.");
            }

            logger.LogWarning("Acquiring bank health check returned null response.");
            return HealthCheckResult.Degraded("Acquiring bank returned no content.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            logger.LogError(ex, "Acquiring bank returned 503 Service Unavailable.");
            return HealthCheckResult.Unhealthy("Acquiring bank is unavailable (503).", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Acquiring bank health check failed with an unexpected error.");
            return HealthCheckResult.Unhealthy("Acquiring bank health check failed.", ex);
        }
    }
}