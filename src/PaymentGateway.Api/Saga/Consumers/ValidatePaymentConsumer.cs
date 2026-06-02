using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;

namespace PaymentGateway.Api.Saga.Consumers
{
    public class ValidatePaymentConsumer(IProcessPaymentCommandValidator validator) : IConsumer<ValidatePayment>
    {
        public async Task Consume(ConsumeContext<ValidatePayment> context)
        {
            var validationErrors = validator.Validate(
                new ProcessPaymentCommand(
                    context.Message.MerchantId,
                    context.Message.CardNumber,
                    context.Message.ExpiryMonth,
                    context.Message.ExpiryYear,
                    context.Message.Currency,
                    context.Message.Amount,
                    context.Message.Cvv,
                    context.Message.IdempotencyKey));

            if (validationErrors.Count == 0)
            {
                await context.Publish(new PaymentValidated(
                    context.Message.CorrelationId,
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
                    new PaymentValidationFailed(context.Message.CorrelationId,
                    PaymentFailureFactory.InvalidPaymentRequest(validationErrors)));
            }
        }
    }
}
