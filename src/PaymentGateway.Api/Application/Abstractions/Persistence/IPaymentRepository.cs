using PaymentGateway.Api.Domain.Entities.Payments;

namespace PaymentGateway.Api.Application.Abstractions.Persistence;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Payment?> GetByIdempotencyKeyAsync(
        Guid merchantId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

    void Update(Payment payment);
}


