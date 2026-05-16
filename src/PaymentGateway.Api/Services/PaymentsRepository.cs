using PaymentGateway.Api.Models.Responses;

using System.Collections.Concurrent;

namespace PaymentGateway.Api.Services;

public class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, PostPaymentResponse> _payments = new();
    private readonly ConcurrentDictionary<string, IdempotencyResult> _idempotency = new(StringComparer.Ordinal);

    public bool TryAdd(PostPaymentResponse payment, string? idempotencyKey = null, string? requestHash = null)
    {
        if (!_payments.TryAdd(payment.Id, payment))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && !string.IsNullOrWhiteSpace(requestHash))
        {
            if (!_idempotency.TryAdd(idempotencyKey, new IdempotencyResult(payment, requestHash)))
            {
                _payments.TryRemove(payment.Id, out _);
                return false;
            }
        }

        return true;
    }

    public PostPaymentResponse? TryGet(Guid paymentId)
    {
        return _payments.TryGetValue(paymentId, out var payment) ? payment : null;
    }

    public IdempotencyResult? TryGetByIdempotencyKey(string idempotencyKey)
    {
        return _idempotency.TryGetValue(idempotencyKey, out var result) ? result : null;
    }
}
