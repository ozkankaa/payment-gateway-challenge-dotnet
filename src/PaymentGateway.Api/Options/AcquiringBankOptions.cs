namespace PaymentGateway.Api.Options;

public sealed class AcquiringBankOptions
{
    public const string SectionName = "AcquiringBank";
    public string BaseUrl { get; init; } = "http://localhost:8080";
    public int MaxRetryAttempts { get; init; } = 3;
    public int TimeoutSeconds { get; init; } = 5;
}
