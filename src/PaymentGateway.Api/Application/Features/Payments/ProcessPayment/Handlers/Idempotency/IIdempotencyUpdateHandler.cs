using PaymentGateway.Api.Application.Abstractions.CQRS;
using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Idempotency
{
    public interface IIdempotencyUpdateHandler : ICommandHandler<IdempotencyUpdateCommand, IdempotencyResult>
    {
    }
}
