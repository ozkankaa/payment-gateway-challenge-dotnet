using PaymentGateway.Api.Infrastructure.Services.FraudService;
using PaymentGateway.Api.Infrastructure.Services.FraudService.Requests;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Fraud
{
    public class FraudCheckHandler(IFraudServiceClient fraudServiceClient, ILogger<FraudCheckHandler> logger) : IFraudCheckHandler
    {
        public async Task<FraudCheckResult> HandleAsync(FraudCheckCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var response = await fraudServiceClient.CheckAsync(
                    new FraudCheckRequest(command.CardNumber),
                    cancellationToken);

                return response is null || !response.Authorized
                    ? new FraudCheckResult
                    {
                        Authorized = false,
                        Error = PaymentFailureFactory.PaymentDeclinedByFraudService()
                    }
                    : new FraudCheckResult
                {
                    Authorized = true
                };
            }
            catch (Exception ex) when (PaymentServiceExceptionHandler.IsServiceUnavailable(ex))
            {
                logger.LogError(
                    ex,
                    "Fraud service call failed. ExceptionType: {ExceptionType}",
                    ex.GetType().FullName);

                return new FraudCheckResult
                {
                    Authorized = false,
                    Error = PaymentFailureFactory.FraudServiceUnavailable()
                };
            }
        }
    }
}
