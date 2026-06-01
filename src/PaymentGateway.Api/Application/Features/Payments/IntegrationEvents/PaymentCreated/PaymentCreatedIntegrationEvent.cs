namespace PaymentGateway.Api.Application.Features.Payments.IntegrationEvents.PaymentCreated;

public sealed record PaymentCreatedIntegrationEvent(
    Guid PaymentId,
    Guid MerchantId,
    string IdempotencyKey,
    long Amount,
    string Currency);