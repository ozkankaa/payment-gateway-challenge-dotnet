namespace PaymentGateway.Api.Options;

public sealed record PaymentsCacheOptions
{
    public const string SectionName = "PaymentsCache";
    public int DurationSeconds { get; init; } = 60;
    public string Tag { get; init; } = "payments";
    public string[] VaryByQuery { get; init; } = Array.Empty<string>();
}
