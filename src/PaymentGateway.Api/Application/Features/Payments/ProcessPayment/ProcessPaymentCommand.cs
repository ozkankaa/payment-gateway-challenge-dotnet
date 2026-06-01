namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment;

public sealed record ProcessPaymentCommand(
    Guid MerchantId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv,
    string? IdempotencyKey
    );
