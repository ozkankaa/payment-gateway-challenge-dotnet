using MassTransit;

namespace PaymentGateway.Api.Saga;

public class PaymentSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; } = default!;

    public Guid PaymentId { get; set; }

    public Guid MerchantId { get; set; } = default!;

    public string CardToken { get; set; } = default!;
    public string CardLast4 { get; set; } = default!;

    public long Amount { get; set; }
    public string? Currency { get; set; }

    public string IdempotencyKey { get; set; } = default!;
    public string? RequestHash { get; set; }

    public string? FraudDecision { get; set; }

    public string? PspId { get; set; }
    public string? PspTransactionId { get; set; }
    
    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }
}