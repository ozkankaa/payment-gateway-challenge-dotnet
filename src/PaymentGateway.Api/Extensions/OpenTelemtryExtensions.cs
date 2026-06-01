using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using PaymentGateway.Api.Options;

namespace PaymentGateway.Api.Extensions;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddApplicationOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceOptions = configuration
            .GetSection("PaymentService")
            .Get<ServiceOptions>()
            ?? new ServiceOptions();

        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: serviceOptions.Name,
                    serviceVersion: serviceOptions.Version);

                resource.AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = serviceOptions.Environment
                });
            })
            .WithTracing(tracing => tracing
                    .AddSource("PaymentGateway.Api")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint =
                            new Uri($"{serviceOptions.OpenTelemetry.OtlpEndpoint}/v1/traces");

                        options.Protocol =
                            OtlpExportProtocol.HttpProtobuf;
                    }))
            .WithMetrics(metrics => metrics
                    .AddMeter("payment-service")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint =
                            new Uri($"{serviceOptions.OpenTelemetry.OtlpEndpoint}/v1/metrics");

                        options.Protocol =
                            OtlpExportProtocol.HttpProtobuf;
                    }));

        return services;
    }
}