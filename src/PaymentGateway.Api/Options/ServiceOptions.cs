namespace PaymentGateway.Api.Options;

public sealed record ServiceOptions
{
    public const string SectionName = "Service";
    public string Name { get; init; } = "unknown-service";
    public string Version { get; init; } = "1.0.0";
    public string Environment { get; init; } = "local";
    public OutputCache OutputCache { get; init; } = new OutputCache();
    public OpenTelemetry OpenTelemetry { get; init; } = new OpenTelemetry();
    public RateLimit RateLimit { get; init; } = new RateLimit();
}

public sealed record OutputCache
{
    public const string SectionName = "Service.OutputCache";
    public int DurationSeconds { get; init; } = 60;
    public string Tag { get; init; } = "payments";
    public string[] VaryByQuery { get; init; } = [];
}

public sealed record OpenTelemetry()
{
    public const string SectionName = "Service.OpenTelemetry";
    public string OtlpEndpoint { get; init; } = "http://localhost:4318";
}

public sealed record RateLimit()
{
    public const string SectionName = "Service.RateLimit";
    public int PermitLimit { get; init; } = 80;
    public int WindowSeconds { get; init; } = 60;
    public int QueueLimit { get; init; } = 20;
    public string QueueProcessing { get; init; } = "OldestFirst";
}
