using PaymentGateway.Api.Application.Features.Payments.Dtos;

namespace PaymentGateway.Api.Saga.Request.Messages;

public record StartPaymentRequest(
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

public sealed record PaymentSucceededResponse(
    Guid PaymentId,
    PaymentDto Payement);

public sealed record PaymentFailedResponse(
    Guid PaymentId,
    ErrorDto? Error);

public record ValidatePaymentRequest(
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
public record PaymentValidatedResponse(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record PaymentValidationFailedResponse(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);


public record CheckIdempotencyRequest(
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

public record IdempotencyAcceptedResponse(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record DuplicatePaymentDetectedResponse(Guid CorrelationId, Guid PaymentId, PaymentDto? Payment);
public record IdempotencyFailedResponse(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);

public record CheckFraudRequest(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);

public record FraudApprovedResponse(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount,
    string Cvv);
public record FraudRejectedResponse(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);
public record FraudFailedResponse(Guid CorrelationId, Guid PaymentId, ErrorDto? Error);

public record AuthorizePaymentRequest(
    Guid CorrelationId,
    Guid PaymentId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Cvv,
    long? Amount,
    string Currency);

public record PaymentAuthorizedResponse(
    Guid CorrelationId,
    Guid PaymentId,
    string PspId,
    string PspTransactionId,
    string CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string Currency,
    long? Amount);

public record PaymentDeclinedResponse(
    Guid CorrelationId,
    Guid PaymentId,
    ErrorDto? Error);

public record PaymentAuthorizationFailedResponse(
    Guid CorrelationId,
    Guid PaymentId,
    ErrorDto? Error);

public record CapturePaymentRequest(
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

public record PaymentCapturedResponse(
    Guid CorrelationId,
    Guid PaymentId,
    PaymentDto? Payment);

public record PaymentCaptureFailedResponse(
    Guid CorrelationId,
    Guid PaymentId,
    ErrorDto? Error);

public record LedgerPaymentAuthorizedResponse(
    Guid CorrelationId,
    Guid PaymentId,
    string PspId,
    string PspTransactionId,
    decimal Amount,
    string Currency);