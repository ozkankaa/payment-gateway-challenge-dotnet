namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.AcquiringBank
{
    public sealed record AcquiringBankAuthorizeCommand
    (
        string CardNumber,
        int? ExpiryMonth,
        int? ExpiryYear,
        string Currency,
        long? Amount,
        string Cvv
    );
}
