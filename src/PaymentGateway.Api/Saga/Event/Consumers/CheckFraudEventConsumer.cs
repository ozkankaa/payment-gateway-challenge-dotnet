using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Fraud;
using PaymentGateway.Api.Saga.Event.Messages;

namespace PaymentGateway.Api.Saga.Event.Consumers;

public class CheckFraudEventConsumer(IFraudCheckHandler fraudCheckHandler) : IConsumer<CheckFraudEvent>
{
    public async Task Consume(ConsumeContext<CheckFraudEvent> context)
    {
        var response = await fraudCheckHandler.HandleAsync(new FraudCheckCommand(context.Message.CardNumber), context.CancellationToken);

        if (response is null || !response.Authorized)
        {
            await context.Publish(new FraudRejectedEvent(
                context.Message.CorrelationId,
                context.Message.PaymentId,
                response?.Error));
            return;

        }
        await context.Publish(new FraudApprovedEvent(
            context.Message.CorrelationId,
            context.Message.PaymentId,
            context.Message.CardNumber,
            context.Message.ExpiryMonth,
            context.Message.ExpiryYear,
            context.Message.Currency,
            context.Message.Amount,
            context.Message.Cvv));
    }
}
