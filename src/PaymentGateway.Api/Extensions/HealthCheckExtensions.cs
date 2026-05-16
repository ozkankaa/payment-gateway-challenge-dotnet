using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using PaymentGateway.Api.HealthChecks;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Extensions;

internal static class HealthCheckExtensions
{
    public static IServiceCollection AddCustomHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<AcquiringBankHealthCheck>(name: "acquiring-bank", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

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