namespace PaymentGateway.Api.Options;

public sealed record PaymentsRateLimitOptions
{
    public const string SectionName = "PaymentsRateLimit";
    public int PermitLimit { get; init; } = 80;
    public int WindowSeconds { get; init; } = 60;
    public int QueueLimit { get; init; } = 20;
    public string QueueProcessing { get; init; } = "OldestFirst";
}