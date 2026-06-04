using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.AcquiringBank;
using PaymentGateway.Api.Saga.Event.Messages;

namespace PaymentGateway.Api.Saga.Event.Consumers;

public class AuthorizePaymentEventConsumer(AcquiringBankAuthorizeHandler acquiringBankAuthorizeHandler) : IConsumer<AuthorizePaymentEvent>
{
    public async Task Consume(ConsumeContext<AuthorizePaymentEvent> context)
    {
        try
        {
            var bankResponse = await acquiringBankAuthorizeHandler.HandleAsync(CreateAcquiringBankAuthorizeCommand(context.Message),
                context.CancellationToken);

            if (!bankResponse!.Authorized)
            {
                await context.Publish(new PaymentAuthorizationFailedEvent(
                context.Message.CorrelationId,
                context.Message.PaymentId,
                new ErrorDto("payment_declined", "Payment was declined by the acquiring bank")));
                return;
            }

            await context.Publish(new PaymentAuthorizedEvent(
                context.Message.CorrelationId,
                context.Message.PaymentId,
                "acquiring_bank",
                bankResponse.AuthorizationCode!,
                context.Message.CardNumber,
                context.Message.ExpiryMonth,
                context.Message.ExpiryYear,
                context.Message.Currency,
                context.Message.Amount));
        }
        catch (Exception ex) when (PaymentServiceExceptionHandler.IsServiceUnavailable(ex))
        {
            await context.Publish(new PaymentAuthorizationFailedEvent(
                context.Message.CorrelationId,
                context.Message.PaymentId,
                new ErrorDto("service_unavailable", "The acquiring bank service is unavailable")));
        }
    }

    private static AcquiringBankAuthorizeCommand CreateAcquiringBankAuthorizeCommand(AuthorizePaymentEvent command)
    {
        return new AcquiringBankAuthorizeCommand(
            CardNumber: command.CardNumber,
            ExpiryMonth: command.ExpiryMonth,
            ExpiryYear: command.ExpiryYear,            
            Amount: command.Amount!.Value,
            Currency: command.Currency,
            Cvv: command.Cvv);
    }
}