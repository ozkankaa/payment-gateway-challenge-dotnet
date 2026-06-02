using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService;
using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Requests;

namespace PaymentGateway.Api.Saga.Consumers;

public class AuthorizePaymentConsumer : IConsumer<AuthorizePayment>
{
    private readonly IAcquiringBankClient _pspClient;

    public AuthorizePaymentConsumer(IAcquiringBankClient pspClient)
    {
        _pspClient = pspClient;
    }

    public async Task Consume(ConsumeContext<AuthorizePayment> context)
    {
        try
        {
            var bankResponse = await _pspClient.ProcessAsync(CreateBankPaymentRequest(context.Message),
                context.CancellationToken);

            if (bankResponse is null)
            {
                await context.Publish(new PaymentAuthorizationFailed(
                context.Message.CorrelationId,
                new ErrorDto("payment_rejected", "Payment was rejected by the acquiring bank")));
                return;
            }

            if (!bankResponse!.Authorized)
            {
                await context.Publish(new PaymentAuthorizationFailed(
                context.Message.CorrelationId,
                new ErrorDto("payment_declined", "Payment was declined by the acquiring bank")));
                return;
            }

            await context.Publish(new PaymentAuthorized(
                context.Message.CorrelationId,
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
            await context.Publish(new PaymentAuthorizationFailed(
                context.Message.CorrelationId,
                new ErrorDto("service_unavailable", "The acquiring bank service is unavailable")));
        }
    }

    private static BankPaymentRequest CreateBankPaymentRequest(AuthorizePayment command)
    {
        return new BankPaymentRequest(
            CardNumber: command.CardNumber,
            ExpiryDate: $"{command.ExpiryMonth:00}/{command.ExpiryYear}",
            Cvv: command.Cvv,
            Amount: command.Amount!.Value,
            Currency: command.Currency.ToUpperInvariant());
    }
}