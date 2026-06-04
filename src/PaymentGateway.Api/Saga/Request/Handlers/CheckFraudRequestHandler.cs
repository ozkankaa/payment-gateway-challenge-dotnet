using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Fraud;
using PaymentGateway.Api.Saga.Request.Messages;

namespace PaymentGateway.Api.Saga.Request.Handlers;

public class CheckFraudRequestHandler(IFraudCheckHandler fraudCheckHandler) : IConsumer<CheckFraudRequest>
{
    public async Task Consume(ConsumeContext<CheckFraudRequest> context)
    {
        var response = await fraudCheckHandler.HandleAsync(new FraudCheckCommand(context.Message.CardNumber), context.CancellationToken);

        if (response is null || !response.Authorized)
        {
            await context.RespondAsync(new FraudRejectedResponse(
                context.Message.CorrelationId,
                context.Message.PaymentId,
                response?.Error));
            return;

        }
        await context.RespondAsync(new FraudApprovedResponse(
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
