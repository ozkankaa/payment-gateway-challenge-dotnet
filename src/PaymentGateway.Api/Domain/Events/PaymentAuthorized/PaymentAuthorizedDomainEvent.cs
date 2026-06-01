using PaymentGateway.Api.Domain.Abstractions;

namespace PaymentGateway.Api.Domain.Events.PaymentAuthorized;

public sealed record PaymentAuthorizedDomainEvent(
    Guid PaymentId,
    Guid MerchantId,
    string ProviderId
) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}