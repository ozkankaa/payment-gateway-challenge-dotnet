namespace PaymentGateway.Api.Application.Features.Payments.IntegrationEvents.PaymentFailed;

public sealed record PaymentFailedIntegrationEvent(
    Guid PaymentId,
    Guid MerchantId,
    string FailureCode,
    string FailureMessage);