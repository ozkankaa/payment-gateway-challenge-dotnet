using PaymentGateway.Api.Domain.Abstractions;

namespace PaymentGateway.Api.Domain.Events.PaymentCreated;

public sealed record PaymentCreatedDomainEvent(
    Guid PaymentId,
    Guid MerchantId,
    string IdempotencyKey,
    long Amount,
    string Currency
) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}