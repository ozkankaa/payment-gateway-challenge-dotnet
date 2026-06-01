namespace PaymentGateway.Api.Application.Features.Payments.IntegrationEvents.PaymentAuthorized;

public sealed record PaymentAuthorizedIntegrationEvent(
    Guid PaymentId,
    Guid MerchantId,
    string ProviderId);