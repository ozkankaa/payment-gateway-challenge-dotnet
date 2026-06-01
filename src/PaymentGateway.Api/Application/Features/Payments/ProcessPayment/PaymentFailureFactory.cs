using PaymentGateway.Api.Application.Features.Payments.Dtos;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment;

public static class PaymentFailureFactory
{
    public static ErrorDto InvalidPaymentRequest(
        IDictionary<string, string[]> validationErrors)
    {
        return new ErrorDto(
            "payment_rejected",
            "Invalid payment request.",
            validationErrors);
    }

    public static ErrorDto IdempotencyConflict(string idempotencyKey)
    {
        return new ErrorDto(
            "idempotency_conflict",
            $"The Idempotency-Key {idempotencyKey} was already used with a different request body.");
    }

    public static ErrorDto IdempotencyError(
        string idempotencyKey,
        IDictionary<string, string[]>? errors)
    {
        return new ErrorDto(
            "idempotency_error",
            $"The idempotency service for Idempotency-Key {idempotencyKey} raised an error.",
            errors);
    }

    public static ErrorDto PaymentDeclinedByFraudService()
    {
        return new ErrorDto(
            "payment_declined",
            "Fraud service rejected the payment request.");
    }

    public static ErrorDto FraudServiceUnavailable()
    {
        return new ErrorDto(
            "fraud_service_unavailable",
            "Fraud service is unavailable. Try again later.");
    }

    public static ErrorDto AcquiringBankRejected()
    {
        return new ErrorDto(
            "payment_rejected",
            "Acquiring bank rejected the payment request.");
    }

    public static ErrorDto AcquiringBankDeclined()
    {
        return new ErrorDto(
            "payment_declined",
            "Acquiring bank declined the payment request.");
    }

    public static ErrorDto BankUnavailable()
    {
        return new ErrorDto(
            "bank_unavailable",
            "Acquiring bank is unavailable. Try again later.");
    }

    public static ErrorDto PaymentPersistenceFailed()
    {
        return new ErrorDto(
            "payment_failed",
            "Payment could not be stored consistently. Retry with the same Idempotency-Key.");
    }
}