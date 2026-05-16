using PaymentGateway.Api.Application.Abstractions;
using PaymentGateway.Api.Application.Payments.Dtos;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Application.Payments.GetPayment;

public class GetPaymentHandler(IPaymentsRepository paymentsRepository,
    ILogger<GetPaymentHandler> logger) : IQueryHandler<GetPaymentQuery, PaymentOperationResult>
{
    public PaymentOperationResult Handle(GetPaymentQuery query)
    {
        var payment = paymentsRepository.TryGet(query.Id);
        if (payment == null)
        {
            logger.LogInformation("Payment {Id} not found.", query.Id);
            return new PaymentOperationResult(PaymentOperationOutcome.NotFound);
        }
        logger.LogInformation("Payment {Id} retrieved.", query.Id);
        return new PaymentOperationResult(PaymentOperationOutcome.Ok, payment);

    }
}
