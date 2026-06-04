using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.AcquiringBank;
using PaymentGateway.Api.Saga.Request.Messages;

namespace PaymentGateway.Api.Saga.Request.Handlers;

public class AuthorizePaymentRequestHandler(AcquiringBankAuthorizeHandler acquiringBankAuthorizeHandler) : IConsumer<AuthorizePaymentRequest>
{
    public async Task Consume(ConsumeContext<AuthorizePaymentRequest> context)
    {
        try
        {
            var bankResponse = await acquiringBankAuthorizeHandler.HandleAsync(CreateAcquiringBankAuthorizeCommand(context.Message),
               context.CancellationToken);

            if (!bankResponse!.Authorized)
            {
                await context.RespondAsync(new PaymentDeclinedResponse(
                context.Message.CorrelationId,
                context.Message.PaymentId,
                bankResponse.Error));
                return;
            }

            await context.RespondAsync(new PaymentAuthorizedResponse(
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
            await context.RespondAsync(new PaymentAuthorizationFailedResponse(
                context.Message.CorrelationId,
                context.Message.PaymentId,
                new ErrorDto("service_unavailable", "The acquiring bank service is unavailable")));
        }
    }

    private static AcquiringBankAuthorizeCommand CreateAcquiringBankAuthorizeCommand(AuthorizePaymentRequest command)
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