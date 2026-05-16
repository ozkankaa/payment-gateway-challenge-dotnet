namespace PaymentGateway.Api.Application.Payments.ProcessPayment;

public sealed record ProcessPaymentCommand(
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv,
    string? IdempotencyKey
    );
