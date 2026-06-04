
using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Idempotency
{
    public class IdempotencyCheckHandler(IIdempotencyService idempotencyService) : IIdempotencyCheckHandler
    {
        public Task<IdempotencyResult> HandleAsync(IdempotencyCheckCommand command, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            {
                var error = new Dtos.ErrorDto("invalid_idempotency_key", "Idempotency key is required and cannot be empty.");
                return Task.FromResult(new IdempotencyResult(IdempotencyStatus.Error, Error: error));
            }

            if (string.IsNullOrWhiteSpace(command.RequestHash))
            {
                var error = new Dtos.ErrorDto("invalid_request_hash", "Request hash is required and cannot be empty.");
                return Task.FromResult(new IdempotencyResult(IdempotencyStatus.Error, Error: error));
            }

            var idempotencyResult = idempotencyService.TryAdd(
                command.IdempotencyKey,
                command.RequestHash);

            return Task.FromResult(idempotencyResult);
        }
    }
}
