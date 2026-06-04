using PaymentGateway.Api.Application.Features.Payments.Dtos;

namespace PaymentGateway.Api.Saga.Messages;

public record StartPayment(
    Guid CorrelationId,
    Guid PaymentId,
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
public sealed record PaymentSucceeded(
    Guid PaymentId,
    PaymentDto Payement);
public sealed record PaymentFailed(
    Guid PaymentId,
    ErrorDto? Error);

public record ValidatePayment(
    Guid CorrelationId,
    Guid PaymentId,
    Guid MerchantId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv,
    string IdempotencyKey);
public record PaymentValidated(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record PaymentValidationFailed(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);


public record CheckIdempotency(
    Guid CorrelationId,
    Guid PaymentId,
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
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record DuplicatePaymentDetected(Guid CorrelationId, Guid PaymentId, PaymentDto? Payment);
public record IdempotencyFailed(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);

public record CheckFraud(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);

public record FraudApproved(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record FraudRejected(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);
public record FraudFailed(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);

public record AuthorizePayment(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Cvv,
    long? Amount,
    string Currency);

public record PaymentAuthorized(
    Guid CorrelationId,
    Guid PaymentId,
    string PspId,
    string PspTransactionId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount);

public record PaymentDeclined(
    Guid CorrelationId,
    Guid PaymentId,
    ErrorDto? Error);

public record PaymentAuthorizationFailed(
    Guid CorrelationId,
    Guid PaymentId,
    ErrorDto? Error);

public record CapturePayment(
    Guid CorrelationId,
    Guid PaymentId,
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
    Guid PaymentId,
    PaymentDto? Payment);

public record PaymentCaptureFailed(
    Guid CorrelationId,
    Guid PaymentId,
    ErrorDto? Error);

public record LedgerPaymentAuthorized(
    Guid CorrelationId,
    Guid PaymentId,
    string PspId,
    string PspTransactionId,
    decimal Amount,
    string Currency);