using PaymentGateway.Api.Application.Features.Payments.Dtos;

namespace PaymentGateway.Api.Saga.Event.Messages;

public record StartPaymentEvent(
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

public record StartPaymentEventRequest(
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
public sealed record PaymentEventSucceeded(
    Guid PaymentId,
    PaymentDto Payement);

public sealed record PaymentEventFailed(
    Guid PaymentId,
    ErrorDto? Error);

public record ValidatePaymentEvent(
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
public record PaymentValidatedEvent(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record PaymentValidationFailedEvent(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);


public record CheckIdempotencyEvent(
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

public record IdempotencyAcceptedEvent(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record DuplicatePaymentDetectedEvent(Guid CorrelationId, Guid PaymentId, PaymentDto? Payment);
public record IdempotencyFailedEvent(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);

public record CheckFraudEvent(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);

public record FraudApprovedEvent(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record FraudRejectedEvent(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);
public record FraudFailedEvent(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);

public record AuthorizePaymentEvent(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Cvv,
    long? Amount,
    string Currency);

public record PaymentAuthorizedEvent(
    Guid CorrelationId,
    Guid PaymentId,
    string PspId,
    string PspTransactionId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount);

public record PaymentDeclinedEvent(
    Guid CorrelationId,
    Guid PaymentId,
    ErrorDto? Error);

public record PaymentAuthorizationFailedEvent(
    Guid CorrelationId,
    Guid PaymentId,
    ErrorDto? Error);

public record CapturePaymentEvent(
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

public record PaymentCapturedEvent(
    Guid CorrelationId,
    Guid PaymentId,
    PaymentDto? Payment);

public record PaymentCaptureFailedEvent(
    Guid CorrelationId,
    Guid PaymentId,
    ErrorDto? Error);

public record LedgerPaymentAuthorizedEvent(
    Guid CorrelationId,
    Guid PaymentId,
    string PspId,
    string PspTransactionId,
    decimal Amount,
    string Currency);