using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Application.Features.Payments.Dtos;

public sealed record PaymentDto
{
    public Guid Id { get; init; }
    public Guid MerchantId { get; init; }
    public PaymentStatusEnum Status { get; init; }
    public int CardNumberLastFour { get; init; }
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public string Currency { get; init; } = string.Empty;
    public long Amount { get; init; }
}
