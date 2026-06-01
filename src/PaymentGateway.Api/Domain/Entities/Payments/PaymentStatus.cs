namespace PaymentGateway.Api.Domain.Entities.Payments;

public enum PaymentStatus
{
    Failed = 1,
    Created = 2,
    IdempotencyVerified = 3,
    FraudCheckPassed = 4,
    Authorized = 5,
    Captured = 6,
    Settled = 7
}