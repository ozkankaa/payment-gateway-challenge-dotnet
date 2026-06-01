using PaymentGateway.Api.Application.Features.Payments.Dtos;

namespace PaymentGateway.Api.Infrastructure.Services.IdempotencyService;

public interface IIdempotencyService
{
    IdempotencyResult TryAdd(string idempotencyKey, string requestHash);
    IdempotencyResult TryGet(string idempotencyKey);
    IdempotencyResult TryUpdate(PaymentDto payment, string idempotencyKey, string requestHash);
}

public enum IdempotencyStatus
{
    Added,
    Updated,
    Conflict,
    Duplicate,
    Error
}

public sealed record IdempotencyResult(
    IdempotencyStatus Status,
    PaymentDto? Payment = null,
    string? RequestHash = null,
    ErrorDto? Error = null
);

