using PaymentGateway.Api.Application.Features.Payments.Dtos;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Idempotency;

public sealed record IdempotencyUpdateCommand(string IdempotencyKey, string RequestHash, PaymentDto Payment);
