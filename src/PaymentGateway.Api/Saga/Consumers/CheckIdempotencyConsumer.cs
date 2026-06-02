using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;

namespace PaymentGateway.Api.Saga.Consumers;

public class CheckIdempotencyConsumer(IIdempotencyService idempotencyService) : IConsumer<CheckIdempotency>
{
    public async Task Consume(ConsumeContext<CheckIdempotency> context)
    {
        try
        {
            var idempotencyKey = context.Message.IdempotencyKey;
            var requestHash = context.Message.RequestHash;

            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return;

            var idempotencyResult = idempotencyService.TryAdd(
                idempotencyKey,
                requestHash);

            switch (idempotencyResult.Status)
            {
                case IdempotencyStatus.Conflict:
                    await context.Publish(new IdempotencyFailed(context.Message.CorrelationId, idempotencyResult.Error));
                    return;

                case IdempotencyStatus.Duplicate when idempotencyResult.Payment is not null:
                    await context.Publish(new DuplicatePaymentDetected(context.Message.CorrelationId, idempotencyResult.Payment));
                    return;

                case IdempotencyStatus.Error:
                    await context.Publish(new IdempotencyFailed(context.Message.CorrelationId, idempotencyResult.Error));
                    return;
                case IdempotencyStatus.Updated when idempotencyResult.Payment is not null:
                    await context.Publish(new DuplicatePaymentDetected(context.Message.CorrelationId, idempotencyResult.Payment));
                    return;

                default:
                    await context.Publish(new IdempotencyAccepted(
                        context.Message.CorrelationId,
                        context.Message.CardNumber,
                        context.Message.ExpiryMonth,
                        context.Message.ExpiryYear,
                        context.Message.Currency,
                        context.Message.Amount,
                        context.Message.Cvv));
                    return;
            }
        }
        catch (Exception ex) when (PaymentServiceExceptionHandler.IsServiceUnavailable(ex))
        {
            await context.Publish(new IdempotencyFailed(
                context.Message.CorrelationId,
                new ErrorDto("idempotency_error", ex.Message)));
        }
    }
}