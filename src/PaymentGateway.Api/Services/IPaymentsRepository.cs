using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IPaymentsRepository
{
    bool TryAdd(PostPaymentResponse payment, string? idempotencyKey = null, string? requestHash = null);
    PostPaymentResponse? TryGet(Guid paymentId);
    IdempotencyResult? TryGetByIdempotencyKey(string idempotencyKey);
}

public sealed record IdempotencyResult(PostPaymentResponse Payment, string RequestHash);

