using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Options;

using RabbitMQ.Client;

namespace PaymentGateway.Api.HealthChecks;

public sealed class RabbitMqHealthCheck(IOptions<RabbitMqOptions> options) : IHealthCheck
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost
            };

            await using var connection =
                await factory.CreateConnectionAsync(cancellationToken);

            await using var channel =
                await connection.CreateChannelAsync(
                    cancellationToken: cancellationToken);

            return !connection.IsOpen || !channel.IsOpen
                ? HealthCheckResult.Unhealthy(
                    "RabbitMQ connection or channel is closed.")
                : HealthCheckResult.Healthy("RabbitMQ is healthy.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "RabbitMQ is unhealthy.",
                ex);
        }
    }
}