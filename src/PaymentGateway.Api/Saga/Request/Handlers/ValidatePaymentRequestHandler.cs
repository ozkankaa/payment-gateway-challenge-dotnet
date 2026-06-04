using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.PaymentValidation;
using PaymentGateway.Api.Saga.Request.Messages;

namespace PaymentGateway.Api.Saga.Request.Handlers;

public class ValidatePaymentRequestHandler(IPaymentValidationHandler paymentValidationHandler) : IConsumer<ValidatePaymentRequest>
{
    public async Task Consume(ConsumeContext<ValidatePaymentRequest> context)
    {
        var validationErrors = await paymentValidationHandler.HandleAsync(
            new ProcessPaymentCommand(
                context.Message.MerchantId,
                context.Message.CardNumber,
                context.Message.ExpiryMonth,
                context.Message.ExpiryYear,
                context.Message.Currency,
                context.Message.Amount,
                context.Message.Cvv,
                context.Message.IdempotencyKey), context.CancellationToken);

        if (validationErrors.Count == 0)
        {
            await context.RespondAsync(new PaymentValidatedResponse(
                context.Message.CorrelationId,
                context.Message.PaymentId,
                context.Message.CardNumber,
                context.Message.ExpiryMonth,
                context.Message.ExpiryYear,
                context.Message.Currency,
                context.Message.Amount,
                context.Message.Cvv));
        }
        else
        {
            await context.RespondAsync(
                new PaymentValidationFailedResponse(
                    context.Message.CorrelationId,
                    context.Message.PaymentId,
                    PaymentFailureFactory.InvalidPaymentRequest(validationErrors)));
        }
    }
}
