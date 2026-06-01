namespace PaymentGateway.Api.Domain.Abstractions;

public interface IDomainEvent
{
    Guid Id { get; }
    DateTime OccurredAtUtc { get; }
}