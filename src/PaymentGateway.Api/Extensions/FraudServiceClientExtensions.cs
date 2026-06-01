using Microsoft.Extensions.Http.Resilience;

using PaymentGateway.Api.Infrastructure.Services.FraudService;
using PaymentGateway.Api.Middleware;
using PaymentGateway.Api.Options;

using Polly;

namespace PaymentGateway.Api.Extensions;

public static class FraudServiceClientExtensions
{
    public static IServiceCollection AddFraudServiceClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FraudServiceOptions>(configuration.GetSection(FraudServiceOptions.SectionName));
        var fraudServiceOptionsConfig =configuration.GetSection(FraudServiceOptions.SectionName).Get<FraudServiceOptions>() ?? new FraudServiceOptions();
        services.AddHttpClient<IFraudServiceClient, FraudServiceClient>(client =>
        {
            client.BaseAddress = new Uri(fraudServiceOptionsConfig.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(fraudServiceOptionsConfig.TimeoutSeconds);
        })
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddResilienceHandler("fraud-service-client-pipeline", pipeline =>
        {
            // Prevents service from waiting forever
            pipeline.AddTimeout(TimeSpan.FromSeconds(fraudServiceOptionsConfig.TimeoutSeconds));

            // Retry only when the failure may be temporary
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = fraudServiceOptionsConfig.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args =>
                {
                    var response = args.Outcome.Result;

                    if (response is null)
                        return PredicateResult.True();

                    var statusCode = (int)response.StatusCode;

                    return ValueTask.FromResult(
                        statusCode == 408 ||
                        statusCode == 429 ||
                        statusCode == 500 ||
                        statusCode == 502 ||
                        statusCode == 503 ||
                        statusCode == 504);
                }
            });

            // Circuit breaker : Stops calling a failing dependency temporarily (This protects app threads, database connections, and users)
            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 20,
                BreakDuration = TimeSpan.FromSeconds(30)
            });

            // Bulkhead isolation : Limits how many concurrent calls can go to one dependency
            pipeline.AddConcurrencyLimiter(100 /* max concurrent executions */, 50 /* waiting queue size */);
        });
        return services;
    }
}
