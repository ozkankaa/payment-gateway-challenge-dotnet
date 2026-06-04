using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.PaymentValidation;
using PaymentGateway.Api.Saga.Event.Messages;

namespace PaymentGateway.Api.Saga.Event.Consumers
{
    public class ValidatePaymentEventConsumer(IPaymentValidationHandler paymentValidationHandler) : IConsumer<ValidatePaymentEvent>
    {
        public async Task Consume(ConsumeContext<ValidatePaymentEvent> context)
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
                await context.Publish(new PaymentValidatedEvent(
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
                await context.Publish(
                    new PaymentValidationFailedEvent(
                        context.Message.CorrelationId,
                        context.Message.PaymentId,
                        PaymentFailureFactory.InvalidPaymentRequest(validationErrors)));
            }
        }
    }
}
