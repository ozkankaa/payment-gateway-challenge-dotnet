namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.PaymentValidation;

public class PaymentValidationHandler(IProcessPaymentCommandValidator validator) : IPaymentValidationHandler
{
    public Task<IDictionary<string, string[]>> HandleAsync(ProcessPaymentCommand command, CancellationToken cancellationToken)
    {
        var validationErrors = validator.Validate(
            new ProcessPaymentCommand(
                command.MerchantId,
                command.CardNumber,
                command.ExpiryMonth,
                command.ExpiryYear,
                command.Currency,
                command.Amount,
                command.Cvv,
                command.IdempotencyKey));

        return Task.FromResult(validationErrors);
    }
}
