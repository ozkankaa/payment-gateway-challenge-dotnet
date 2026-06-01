namespace PaymentGateway.Api.Application.Features.Payments.IntegrationEvents;

public sealed record IntegrationEvent(
    string MessageType,
        string Content);