using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService;
using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Requests;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.AcquiringBank;

public class AcquiringBankAuthorizeHandler(IAcquiringBankClient acquiringBankClient, ILogger<AcquiringBankAuthorizeHandler> logger) : IAcquiringBankAuthorizeHandler
{
    public async Task<AcquiringBankAuthorizeResult> HandleAsync(AcquiringBankAuthorizeCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var bankResponse = await acquiringBankClient.ProcessAsync(
                CreateBankPaymentRequest(command),
                cancellationToken);

            if (bankResponse is null)
            {
                return new AcquiringBankAuthorizeResult() { 
                    Authorized = false,
                    AuthorizationCode = null,
                    Error = new ErrorDto
                    (
                        Code : "AcquiringBankError",
                        Message : "Failed to process payment with acquiring bank."
                    )
                };
            }

            if (!bankResponse.Authorized)
            {
                return new AcquiringBankAuthorizeResult()
                {
                    Authorized = false,
                    AuthorizationCode = null,
                    Error = PaymentFailureFactory.AcquiringBankDeclined()
                };
            }

            return new AcquiringBankAuthorizeResult()
            {
                Authorized = true,
                AuthorizationCode = bankResponse.AuthorizationCode,
                Error = null
            };
        }
        catch (Exception ex) when (PaymentServiceExceptionHandler.IsServiceUnavailable(ex))
        {
            logger.LogError(
                ex,
                "Acquiring bank call failed. ExceptionType: {ExceptionType}",
                ex.GetType().FullName);

            return new AcquiringBankAuthorizeResult()
            {
                Authorized = false,
                AuthorizationCode = null,
                Error = PaymentFailureFactory.BankUnavailable()
            };
        }
    }

    private static BankPaymentRequest CreateBankPaymentRequest(AcquiringBankAuthorizeCommand command)
    {
        return new BankPaymentRequest(
            CardNumber: command.CardNumber,
            ExpiryDate: $"{command.ExpiryMonth:00}/{command.ExpiryYear}",
            Cvv: command.Cvv,
            Amount: command.Amount!.Value,
            Currency: command.Currency.ToUpperInvariant());
    }
}
