using Microsoft.EntityFrameworkCore;

using PaymentGateway.Api.Application.Abstractions.Persistence;
using PaymentGateway.Api.Domain.Entities.Payments;

namespace PaymentGateway.Api.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(PaymentDbContext dbContext) : IPaymentRepository
{
    private readonly PaymentDbContext _dbContext = dbContext;

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Payments
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Payment?> GetByIdempotencyKeyAsync(
        Guid merchantId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Payments
            .FirstOrDefaultAsync(
                x => x.MerchantId == merchantId &&
                     x.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        await _dbContext.Payments.AddAsync(payment, cancellationToken);
    }

    public void Update(Payment payment)
    {
        _dbContext.Payments.Update(payment);
    }
}
