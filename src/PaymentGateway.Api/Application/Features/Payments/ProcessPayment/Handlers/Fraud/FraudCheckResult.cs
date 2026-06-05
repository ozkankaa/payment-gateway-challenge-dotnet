using PaymentGateway.Api.Application.Features.Payments.Dtos;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Fraud;

public sealed record FraudCheckResult
{
    public bool Authorized { get; set; }
    public string? AuthorizationCode { get; set; }
    public ErrorDto? Error { get; set; }
}
