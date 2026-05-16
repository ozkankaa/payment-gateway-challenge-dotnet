namespace PaymentGateway.Api.Models.Responses;

public sealed record PostPaymentResponse
{
    public Guid Id { get; init; }
    public PaymentStatus Status { get; init; }
    public int CardNumberLastFour { get; init; }
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public string Currency { get; init; } = string.Empty;
    public long Amount { get; init; }
};
