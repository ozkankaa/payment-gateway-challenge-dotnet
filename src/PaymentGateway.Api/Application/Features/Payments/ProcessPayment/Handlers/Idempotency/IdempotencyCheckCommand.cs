namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Idempotency;

public sealed record IdempotencyCheckCommand(string IdempotencyKey, string RequestHash);
