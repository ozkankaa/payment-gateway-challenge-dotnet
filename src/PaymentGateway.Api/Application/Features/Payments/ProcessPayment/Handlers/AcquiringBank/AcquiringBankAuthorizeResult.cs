using PaymentGateway.Api.Application.Features.Payments.Dtos;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.AcquiringBank
{
    public sealed record AcquiringBankAuthorizeResult
    {
        public bool Authorized { get; init; }
        public string? AuthorizationCode { get; init; }
        public ErrorDto? Error { get; set; }
    }
}
