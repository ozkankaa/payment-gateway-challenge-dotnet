using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Infrastructure.Services.FraudService;
using PaymentGateway.Api.Infrastructure.Services.FraudService.Requests;

namespace PaymentGateway.Api.Saga.Consumers;

public class CheckFraudConsumer(IFraudServiceClient fraudServiceClient) : IConsumer<CheckFraud>
{
    public async Task Consume(ConsumeContext<CheckFraud> context)
    {
        try
        { 
            var response = await fraudServiceClient.CheckAsync(new FraudCheckRequest(context.Message.CardNumber));

            if (response is null || !response.Authorized)
            {
                await context.Publish(new FraudRejected(
                    context.Message.CorrelationId, 
                    context.Message.PaymentId,
                    new ErrorDto("fraud_rejected", "Fraud service rejected payment")));
                return;

            }
            await context.Publish(new FraudApproved(
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
        catch (Exception ex) when (PaymentServiceExceptionHandler.IsServiceUnavailable(ex))
        {
            await context.Publish(new FraudFailed(
                context.Message.CorrelationId,
                context.Message.PaymentId,
                new ErrorDto("fraud_service_error", ex.Message)));
        }
    }
}
