using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using PaymentGateway.Api.HealthChecks;
using PaymentGateway.Api.Options;

namespace PaymentGateway.Api.Extensions;

internal static class HealthCheckExtensions
{
    public static IServiceCollection AddCustomHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<AcquiringBankHealthCheck>(name: "acquiring-bank", failureStatus: HealthStatus.Unhealthy, tags: ["external-service", "acquiring-bank", "ready"])
            .AddCheck<AcquiringBankHealthCheck>(name: "fraud-service", failureStatus: HealthStatus.Unhealthy, tags: ["external-service", "fraud-service", "ready"])
            .AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", failureStatus: HealthStatus.Unhealthy, tags: ["messaging", "rabbitmq", "ready"])
            .AddCheck<PaymentDbContextHealthCheck>(name: "payment-db", HealthStatus.Unhealthy, tags: ["database", "payment-db", "ready"]); ;


        return services;
    }

    public static IEndpointConventionBuilder MapHealthChecksEndpoints(this IEndpointRouteBuilder app)
    {
        var live = app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = (check) => check.Tags.Contains("live"),
            ResponseWriter = WriteHealthCheckResponse
        });

        var ready = app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = (check) => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthCheckResponse
        });

        return live;
    }

    private static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                exception = entry.Value.Exception?.Message,
                duration = entry.Value.Duration.ToString()
            })
        };

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}