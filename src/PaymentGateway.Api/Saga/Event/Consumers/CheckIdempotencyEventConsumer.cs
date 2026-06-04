using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Idempotency;
using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;
using PaymentGateway.Api.Saga.Event.Messages;

namespace PaymentGateway.Api.Saga.Event.Consumers;

public class CheckIdempotencyEventConsumer(IIdempotencyCheckHandler idempotencyCheckHandler) : IConsumer<CheckIdempotencyEvent>
{
    public async Task Consume(ConsumeContext<CheckIdempotencyEvent> context)
    {
        var idempotencyKey = context.Message.IdempotencyKey;
        var requestHash = context.Message.RequestHash;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return;

        var idempotencyResult = await idempotencyCheckHandler.HandleAsync(new IdempotencyCheckCommand(
            idempotencyKey,
            requestHash), context.CancellationToken);

        switch (idempotencyResult.Status)
        {
            case IdempotencyStatus.Conflict:
                await context.Publish(new IdempotencyFailedEvent(
                    context.Message.CorrelationId,
                    context.Message.PaymentId,
                    idempotencyResult.Error));
                return;

            case IdempotencyStatus.Duplicate when idempotencyResult.Payment is not null:
                await context.Publish(new DuplicatePaymentDetectedEvent(
                    context.Message.CorrelationId,
                    context.Message.PaymentId,
                    idempotencyResult.Payment));
                return;

            case IdempotencyStatus.Error:
                await context.Publish(new IdempotencyFailedEvent(
                    context.Message.CorrelationId,
                    context.Message.PaymentId,
                    idempotencyResult.Error));
                return;
            case IdempotencyStatus.Updated when idempotencyResult.Payment is not null:
                await context.Publish(new DuplicatePaymentDetectedEvent(
                    context.Message.CorrelationId,
                    context.Message.PaymentId,
                    idempotencyResult.Payment));
                return;

            default:
                await context.Publish(new IdempotencyAcceptedEvent(
                    context.Message.CorrelationId,
                    context.Message.PaymentId,
                    context.Message.CardNumber,
                    context.Message.ExpiryMonth,
                    context.Message.ExpiryYear,
                    context.Message.Currency,
                    context.Message.Amount,
                    context.Message.Cvv));
                return;
        }
    }
}