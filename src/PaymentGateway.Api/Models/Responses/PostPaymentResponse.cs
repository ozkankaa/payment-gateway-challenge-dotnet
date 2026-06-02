namespace PaymentGateway.Api.Models.Responses;

public sealed record PostPaymentResponse
{
    public Guid Id { get; init; }
    public PaymentStatusEnum Status { get; init; }
    public string CardNumberLastFour { get; init; } = string.Empty;
    public string ExpiryMonth { get; init; } = string.Empty;
    public string ExpiryYear { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public long Amount { get; init; }
};
