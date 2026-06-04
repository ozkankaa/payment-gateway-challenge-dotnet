using MassTransit;

using PaymentGateway.Api.Application.Abstractions.Persistence;
using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.Mappers;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Idempotency;
using PaymentGateway.Api.Domain.Entities.Payments;
using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;
using PaymentGateway.Api.Saga.Request.Messages;

namespace PaymentGateway.Api.Saga.Request.Handlers;

public class CapturePaymentRequestHandler(
    IPaymentRepository paymentRepository, 
    IUnitOfWork unitOfWork,
    IIdempotencyUpdateHandler idempotencyUpdateHandler,
    ILogger<CapturePaymentRequestHandler> logger) : IConsumer<CapturePaymentRequest>
{

    public async Task Consume(ConsumeContext<CapturePaymentRequest> context)
    {
        try
        {
            var payment = Payment.Create(
                    context.Message.PaymentId,
                    context.Message.IdempotencyKey,
                    context.Message.MerchantId,
                    CardDetails.Create(
                        context.Message.CardNumber[^4..],
                        context.Message.ExpiryMonth!.Value,
                        context.Message.ExpiryYear!.Value),
                    Money.Create(
                        context.Message.Amount!.Value,
                        context.Message.Currency));

            payment.MarkAsIdempotencyVerified();
            payment.MarkAsFraudCheckPassed();
            payment.MarkAsAuthorized(context.Message.PspId, context.Message.PspTransactionId);
            payment.MarkAsCaptured();

            await paymentRepository.AddAsync(payment);

            await unitOfWork.SaveChangesAsync();

            var paymentDto = payment.ToDto();

            await UpdateIdempotencyStoreIfRequired(paymentDto, context.Message.IdempotencyKey, context.Message.RequestHash, context.CancellationToken);

            await context.RespondAsync(new PaymentCapturedResponse(
                CorrelationId: context.Message.CorrelationId,
                PaymentId: paymentDto.Id,
                Payment : paymentDto));
        }
        catch (Exception ex)
        {
            await context.RespondAsync(new PaymentCaptureFailedResponse(
                CorrelationId: context.Message.CorrelationId,
                PaymentId: context.Message.PaymentId,
                Error: new ErrorDto("payment_capture_failed",ex.Message)));
        }
    }

    private async Task UpdateIdempotencyStoreIfRequired(PaymentDto paymentDto, string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return;

        var result = await idempotencyUpdateHandler.HandleAsync(new IdempotencyUpdateCommand(
            idempotencyKey,
            requestHash,
            paymentDto),
            cancellationToken);

        if (result.Status == IdempotencyStatus.Updated)
            return;

        logger.LogWarning(
            "Payment {PaymentId} processed with status {Status} but could not be updated in idempotency service with key {IdempotencyKey}.",
            paymentDto.Id,
            paymentDto.Status,
            idempotencyKey);
    }

}
