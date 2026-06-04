
namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Fraud;

public sealed record FraudCheckCommand(string CardNumber);
