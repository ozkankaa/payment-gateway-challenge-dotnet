using PaymentGateway.Api.Domain.Abstractions;

namespace PaymentGateway.Api.Domain.Events.PaymentFailed;

public sealed record PaymentFailedDomainEvent(
    Guid PaymentId,
    Guid MerchantId,
    string FailureCode,
    string FailureMessage
) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}