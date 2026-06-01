namespace PaymentGateway.Api.Options;

public sealed class FraudServiceOptions
{
    public const string SectionName = "FraudService";
    public string BaseUrl { get; init; } = "http://localhost:8081";
    public int MaxRetryAttempts { get; init; } = 3;
    public int TimeoutSeconds { get; init; } = 5;
}
