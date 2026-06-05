using Microsoft.Extensions.Diagnostics.HealthChecks;

using PaymentGateway.Api.Infrastructure.Persistence;

namespace PaymentGateway.Api.HealthChecks;

public sealed class PaymentDbContextHealthCheck(IServiceScopeFactory serviceScopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope =
                serviceScopeFactory.CreateAsyncScope();

            var dbContext =
                scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

            var canConnect =
                await dbContext.Database.CanConnectAsync(cancellationToken);

            return !canConnect
                ? HealthCheckResult.Unhealthy(
                    "Payment database is not reachable.")
                : HealthCheckResult.Healthy(
                "Payment database is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Payment database health check failed.",
                ex);
        }
    }
}
