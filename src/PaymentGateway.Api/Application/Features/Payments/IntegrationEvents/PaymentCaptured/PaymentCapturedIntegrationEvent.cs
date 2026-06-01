using PaymentGateway.Api.Domain.Abstractions;

namespace PaymentGateway.Api.Application.Features.Payments.IntegrationEvents.PaymentCaptured;

public sealed record PaymentCapturedIntegrationEvent(
    Guid PaymentId,
    Guid MerchantId
) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}