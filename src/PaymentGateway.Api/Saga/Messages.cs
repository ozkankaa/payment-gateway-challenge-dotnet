using PaymentGateway.Api.Application.Features.Payments.Dtos;

namespace PaymentGateway.Api.Saga;

public record StartPayment(
    Guid CorrelationId,
    Guid MerchantId,
    string CardToken,
    string CardLast4,
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    long Amount,
    string Cvv,
    string IdempotencyKey,
    string RequestHash);

public record ValidatePayment(
    Guid CorrelationId,
    Guid MerchantId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv,
    string IdempotencyKey);
public record PaymentValidated(Guid CorrelationId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record PaymentValidationFailed(Guid CorrelationId, ErrorDto? Error);


public record CheckIdempotency(
    Guid CorrelationId,
    string IdempotencyKey,
    string RequestHash,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);

public record IdempotencyAccepted(
    Guid CorrelationId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record DuplicatePaymentDetected(Guid CorrelationId, PaymentDto Payment);
public record IdempotencyFailed(Guid CorrelationId, ErrorDto? Error);

public record CheckFraud(
    Guid CorrelationId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);

public record FraudApproved(
    Guid CorrelationId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record FraudRejected(Guid CorrelationId, ErrorDto? Error);
public record FraudFailed(Guid CorrelationId, ErrorDto? Error);

public record AuthorizePayment(
    Guid CorrelationId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Cvv,
    long? Amount,
    string Currency);

public record PaymentAuthorized(
    Guid CorrelationId,
    string PspId,
    string PspTransactionId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount);

public record PaymentAuthorizationFailed(
    Guid CorrelationId,
    ErrorDto? Error);

public record CapturePayment(
    Guid CorrelationId,
    Guid MerchantId,
    string PspId,
    string PspTransactionId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string IdempotencyKey,
    string RequestHash);

public record PaymentCaptured(
    Guid CorrelationId,
    Guid PaymentId);

public record PaymentCapureFailed(
    Guid CorrelationId,
    ErrorDto? Error);

public record LedgerPaymentAuthorized(
    Guid CorrelationId,
    Guid PaymentId,
    string PspId,
    string PspTransactionId,
    decimal Amount,
    string Currency);