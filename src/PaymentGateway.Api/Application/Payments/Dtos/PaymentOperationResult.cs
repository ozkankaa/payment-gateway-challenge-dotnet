using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Application.Payments.Dtos;

public enum PaymentOperationOutcome
{
    Created,
    Ok,
    NotModified,
    NotFound,
    BadRequest,
    Conflict,
    ServiceUnavailable
}

public sealed record PaymentOperationResult(
    PaymentOperationOutcome Outcome,
    PostPaymentResponse? Payment = null,
    ErrorResponse? Error = null,
    string? ETag = null);