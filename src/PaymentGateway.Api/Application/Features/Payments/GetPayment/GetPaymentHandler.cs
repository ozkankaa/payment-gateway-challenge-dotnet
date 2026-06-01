using PaymentGateway.Api.Application.Abstractions.CQRS;
using PaymentGateway.Api.Application.Abstractions.Persistence;
using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.Mappers;

namespace PaymentGateway.Api.Application.Features.Payments.GetPayment;

public class GetPaymentHandler(IPaymentRepository paymentsRepository) : IQueryHandler<GetPaymentQuery, PaymentOperationResultDto>
{
    public async Task<PaymentOperationResultDto> HandleAsync(GetPaymentQuery query, CancellationToken cancellationToken)
    {
        var payment = await paymentsRepository.GetByIdAsync(query.Id, cancellationToken);

        return payment == null
            ? new PaymentOperationResultDto()
            {
                Outcome = PaymentOperationOutcome.NotFound,
                Error = new ErrorDto("payment_not_found", $"Payment {query.Id} not found.")
            }
            : new PaymentOperationResultDto()
            {
                Outcome = PaymentOperationOutcome.Ok,
                Payment = payment.ToDto()
            };
    }
}
