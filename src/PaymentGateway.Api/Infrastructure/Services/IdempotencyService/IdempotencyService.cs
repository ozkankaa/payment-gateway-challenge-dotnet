using System.Collections.Concurrent;

using PaymentGateway.Api.Application.Features.Payments.Dtos;

namespace PaymentGateway.Api.Infrastructure.Services.IdempotencyService;

public class IdempotencyService : IIdempotencyService
{
    private static readonly ConcurrentDictionary<string, IdempotencyResult> Idempotency = new(StringComparer.Ordinal);

    public IdempotencyResult TryAdd(string idempotencyKey, string requestHash)
    {
        var validationResult = ValidateQueries(idempotencyKey, requestHash);

        if (validationResult.Count > 0)
        {
            return new IdempotencyResult(
                IdempotencyStatus.Error,
                null,
                requestHash,
                new ErrorDto("idempotency_validation_error", "Idempotency raised validation errors.")
            );
        }

        var newIdempotencyResult = new IdempotencyResult(IdempotencyStatus.Added, null, requestHash, null);

        if (!Idempotency.TryAdd(idempotencyKey, newIdempotencyResult))
        {
            var existingIdempotencyResult = TryGet(idempotencyKey);

            return existingIdempotencyResult.Status == IdempotencyStatus.Added
                && existingIdempotencyResult.RequestHash == requestHash
                ? existingIdempotencyResult
                : existingIdempotencyResult.RequestHash != requestHash
                ? new IdempotencyResult(
                    IdempotencyStatus.Conflict,
                    null,
                    requestHash,
                    new ErrorDto("idempotency_conflict", "Idempotency raised conflict.")
                )
                : new IdempotencyResult(
                    IdempotencyStatus.Duplicate,
                    existingIdempotencyResult.Payment,
                    requestHash,
                    new ErrorDto("idempotency_exists", "Idempotency raised duplicate."));
        }

        return newIdempotencyResult;
    }

    public IdempotencyResult TryGet(string idempotencyKey)
    {
        return Idempotency.TryGetValue(idempotencyKey, out IdempotencyResult? idempotencyResult)
            ? idempotencyResult
            : new IdempotencyResult(
            IdempotencyStatus.Error,
            null,
            null,
            new ErrorDto(Code: "idempotency_notfound", Message: $"Requested idempotency key {idempotencyKey} not found."));
    }

    public IdempotencyResult TryUpdate(PaymentDto payment, string idempotencyKey, string requestHash)
    {
        var validationResult = ValidateQueries(idempotencyKey, requestHash);

        if (validationResult.Count > 0)
        {
            return new IdempotencyResult(
                IdempotencyStatus.Error,
                payment,
                requestHash,
                new ErrorDto("idempotency_validation_error", "Idempotency raised validation errors.")
            );
        }

        var existingIdempotencyResult = TryGet(idempotencyKey);

        if (existingIdempotencyResult.Status == IdempotencyStatus.Error)
        {
            return existingIdempotencyResult;
        }

        if (!string.Equals(existingIdempotencyResult.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return new IdempotencyResult(
                IdempotencyStatus.Conflict,
                payment,
                requestHash,
                new ErrorDto("idempotency_conflict", $"The Idempotency-Key {idempotencyKey} was already used with a different request body.")
            );
        }

        var newIdempotencyResult = existingIdempotencyResult with
        {
            Payment = payment,
            Status = IdempotencyStatus.Updated
        };

        return !Idempotency.TryUpdate(idempotencyKey, newIdempotencyResult, existingIdempotencyResult)
            ? new IdempotencyResult(
                IdempotencyStatus.Error,
                payment,
                requestHash,
                new ErrorDto("idempotency_error", "Idempotency raised error on update.")
            )
            : newIdempotencyResult;
    }

    private static Dictionary<string, List<string>> ValidateQueries(string idempotencyKey, string requestHash)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                errors[field] = messages = [];
            }
            messages.Add(message);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            Add(nameof(idempotencyKey), "Idempotency key is required");
        }

        if (string.IsNullOrWhiteSpace(requestHash))
        {
            Add(nameof(requestHash), "Request hash is required");
        }

        return errors;
    }

}
