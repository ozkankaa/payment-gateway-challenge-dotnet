using MassTransit;

using PaymentGateway.Api.Application.Abstractions.Persistence;
using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.Mappers;
using PaymentGateway.Api.Domain.Entities.Payments;
using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;

namespace PaymentGateway.Api.Saga.Consumers;

public class CapturePaymentConsumer(
    IPaymentRepository paymentRepository, 
    IUnitOfWork unitOfWork, 
    IIdempotencyService idempotencyService,
    ILogger<CapturePaymentConsumer> logger) : IConsumer<CapturePayment>
{

    public async Task Consume(ConsumeContext<CapturePayment> context)
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

            UpdateIdempotencyStoreIfRequired(paymentDto, context.Message.IdempotencyKey, context.Message.RequestHash);

            await context.Publish(new PaymentCaptured(
                CorrelationId: context.Message.CorrelationId,
                PaymentId: paymentDto.Id));
        }
        catch (Exception ex)
        {
            await context.Publish(new PaymentCapureFailed(
                CorrelationId: context.Message.CorrelationId,
                PaymentId: context.Message.PaymentId,
                Error: new ErrorDto("payment_capture_failed",ex.Message)));
        }
    }

    private void UpdateIdempotencyStoreIfRequired(PaymentDto paymentDto, string idempotencyKey, string requestHash)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return;

        var result = idempotencyService.TryUpdate(
            paymentDto,
            idempotencyKey,
            requestHash);

        if (result.Status == IdempotencyStatus.Updated)
            return;

        logger.LogWarning(
            "Payment {PaymentId} processed with status {Status} but could not be updated in idempotency service with key {IdempotencyKey}.",
            paymentDto.Id,
            paymentDto.Status,
            idempotencyKey);
    }

}
