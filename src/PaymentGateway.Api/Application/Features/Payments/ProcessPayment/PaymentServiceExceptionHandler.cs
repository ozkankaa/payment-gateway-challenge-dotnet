using System.Net;

using Polly.CircuitBreaker;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment;

public static class PaymentServiceExceptionHandler
{
    public static bool IsServiceUnavailable(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpException
                when httpException.StatusCode == HttpStatusCode.ServiceUnavailable => true,

            TaskCanceledException => true,

            TimeoutException => true,

            BrokenCircuitException => true,

            _ => false
        };
    }
}