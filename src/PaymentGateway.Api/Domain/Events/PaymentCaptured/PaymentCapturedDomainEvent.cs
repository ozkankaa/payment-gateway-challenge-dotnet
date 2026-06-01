using PaymentGateway.Api.Domain.Abstractions;

namespace PaymentGateway.Api.Domain.Events.PaymentCaptured;

public sealed record PaymentCapturedDomainEvent(
    Guid PaymentId,
    Guid MerchantId
) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}