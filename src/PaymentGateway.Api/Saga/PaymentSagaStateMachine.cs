using MassTransit;

namespace PaymentGateway.Api.Saga;

public class PaymentSagaStateMachine : MassTransitStateMachine<PaymentSagaState>
{
    public State ValidatePayment { get; private set; } = default!;
    public State CheckingIdempotency { get; private set; } = default!;
    public State CheckingFraud { get; private set; } = default!;
    public State AuthorizingPayment { get; private set; } = default!;
    public State CapturingPayment { get; private set; } = default!;
    public State Completed { get; private set; } = default!;
    public State Failed { get; private set; } = default!;

    public Event<StartPayment> PaymentStarted { get; private set; } = default!;

    public Event<PaymentValidated> PaymentValidated { get; private set; } = default!;
    public Event<PaymentValidationFailed> PaymentValidationFailed { get; private set; } = default!;

    public Event<IdempotencyAccepted> IdempotencyAccepted { get; private set; } = default!;
    public Event<DuplicatePaymentDetected> DuplicateDetected { get; private set; } = default!;
    public Event<IdempotencyFailed> IdempotencyFailed { get; private set; } = default!;

    public Event<FraudApproved> FraudApproved { get; private set; } = default!;
    public Event<FraudRejected> FraudRejected { get; private set; } = default!;
    public Event<FraudFailed> FraudFailed { get; private set; } = default!;

    public Event<PaymentAuthorized> PaymentAuthorized { get; private set; } = default!;
    public Event<PaymentAuthorizationFailed> PaymentAuthorizationFailed { get; private set; } = default!;

    public Event<PaymentCaptured> PaymentCaptured { get; private set; } = default!;
    public Event<PaymentCapureFailed> PaymentCaptureFailed { get; private set; } = default!;

    public PaymentSagaStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => PaymentStarted, x =>
        {
            x.CorrelateById(context => context.Message.CorrelationId);
            x.InsertOnInitial = true;
            x.SetSagaFactory(context => new PaymentSagaState
            {
                CorrelationId = context.Message.CorrelationId,
                CurrentState = "Initial",
                MerchantId = context.Message.MerchantId,
                CardToken = context.Message.CardToken,
                CardLast4 = context.Message.CardLast4,
                Amount = context.Message.Amount,
                Currency = context.Message.Currency,
                IdempotencyKey = context.Message.IdempotencyKey,
                RequestHash = context.Message.RequestHash,
                CreatedAt = DateTime.UtcNow
            });
        });

        Event(() => IdempotencyAccepted, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => DuplicateDetected, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => IdempotencyFailed, x => x.CorrelateById(m => m.Message.CorrelationId));

        Event(() => FraudApproved, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => FraudRejected, x => x.CorrelateById(m => m.Message.CorrelationId));

        Event(() => PaymentAuthorized, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => PaymentAuthorizationFailed, x => x.CorrelateById(m => m.Message.CorrelationId));

        Event(() => PaymentCaptured, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => PaymentCaptureFailed, x => x.CorrelateById(m => m.Message.CorrelationId));

        Initially(
            When(PaymentStarted)
                .Then(ctx =>
                {
                    Console.WriteLine($"Saga started: {ctx.Message.IdempotencyKey}");
                })
                .Send(new Uri("queue:validate-payment"), ctx =>
                    new ValidatePayment(ctx.Saga.CorrelationId,
                                            ctx.Saga.MerchantId,
                                            ctx.Message.CardNumber,
                                            ctx.Message.ExpiryMonth,
                                            ctx.Message.ExpiryYear,
                                            ctx.Message.Currency,
                                            ctx.Message.Amount,
                                            ctx.Message.Cvv,
                                            ctx.Saga.IdempotencyKey))
                .TransitionTo(ValidatePayment)
        );

        During(ValidatePayment,
            When(PaymentValidated)
            .Then(ctx =>
            {
                
            })
            .Send(new Uri("queue:check-idempotency"), ctx =>
                new CheckIdempotency(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.IdempotencyKey,
                    ctx.Saga.RequestHash,
                    ctx.Message.CardNumber,
                    ctx.Message.ExpiryMonth,
                    ctx.Message.ExpiryYear,
                    ctx.Message.Currency,
                    ctx.Message.Amount,
                    ctx.Message.Cvv))
            .TransitionTo(CheckingIdempotency),

            When(PaymentValidationFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureCode = ctx.Message?.Error?.Code ?? "validation_failed";
                    ctx.Saga.FailureReason = ctx.Message?.Error?.ToString();
                })
                .TransitionTo(Failed)
                .Finalize()
        );

        During(CheckingIdempotency,
            When(IdempotencyAccepted)
            .Then(ctx =>
            {
            })
            .Send(new Uri("queue:check-fraud"), ctx =>
                new CheckFraud(
                    ctx.Saga.CorrelationId,
                    ctx.Message.CardNumber,
                    ctx.Message.ExpiryMonth,
                    ctx.Message.ExpiryYear,
                    ctx.Message.Currency,
                    ctx.Message.Amount,
                    ctx.Message.Cvv))
            .TransitionTo(CheckingFraud),

            When(DuplicateDetected)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentId = ctx.Message.Payment.Id;
                })
                .TransitionTo(Completed)
                .Finalize(),

            When(IdempotencyFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureCode = ctx.Message?.Error?.Code ?? "idempotency_failed";
                    ctx.Saga.FailureReason = ctx.Message?.Error?.ToString();
                })
                .TransitionTo(Failed)
                .Finalize()
        );

        During(CheckingFraud,
            When(FraudApproved)
                .Then(ctx =>
                {
                    ctx.Saga.FraudDecision = "Approved";
                })
                .Send(new Uri("queue:authorize-payment"), ctx =>
                    new AuthorizePayment(
                        ctx.Saga.CorrelationId,
                        ctx.Message.CardNumber,
                        ctx.Message.ExpiryMonth,
                        ctx.Message.ExpiryYear,
                        ctx.Message.Cvv,
                        ctx.Message.Amount,
                        ctx.Message.Currency))
                .TransitionTo(AuthorizingPayment),

            When(FraudRejected)
                .Then(ctx =>
                {
                    ctx.Saga.FraudDecision = ctx.Message?.Error?.Code;
                    ctx.Saga.FailureReason = ctx.Message?.Error?.ToString();
                })
                .TransitionTo(Failed)
                .Finalize(),

            When(FraudFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FraudDecision = ctx.Message?.Error?.Code;
                    ctx.Saga.FailureReason = ctx.Message?.Error?.ToString();
                })
                .TransitionTo(Failed)
                .Finalize()
        );

        During(AuthorizingPayment,
            When(PaymentAuthorized)
                .Then(ctx =>
                {
                    ctx.Saga.PspId = ctx.Message.PspId;
                    ctx.Saga.PspTransactionId = ctx.Message.PspTransactionId;
                })
                .Publish(ctx =>
                    new CapturePayment(
                        ctx.Saga.CorrelationId,
                        ctx.Saga.MerchantId,
                        ctx.Saga.PspId!,
                        ctx.Saga.PspTransactionId!,
                        ctx.Message.CardNumber,
                        ctx.Message.ExpiryMonth,
                        ctx.Message.ExpiryYear,
                        ctx.Saga.Currency,
                        ctx.Saga.Amount,
                        ctx.Saga.IdempotencyKey,
                        ctx.Saga.RequestHash
                        ))
                .TransitionTo(CapturingPayment)
                .Finalize(),

            When(PaymentAuthorizationFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureCode = ctx.Message?.Error?.Code ?? "payment_authorization_failed";
                    ctx.Saga.FailureReason = ctx.Message?.Error?.ToString();
                })
                .TransitionTo(Failed)
                .Finalize()
        );


        During(CapturingPayment,
            When(PaymentCaptured)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentId = ctx.Message.PaymentId;
                })
                .Publish(ctx =>
                    new LedgerPaymentAuthorized(
                        ctx.Saga.CorrelationId,
                        ctx.Saga.PaymentId,
                        ctx.Saga.PspId!,
                        ctx.Saga.PspTransactionId!,
                        ctx.Saga.Amount,
                        ctx.Saga.Currency))
                .TransitionTo(Completed)
                .Finalize(),

            When(PaymentCaptureFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureCode = ctx.Message?.Error?.Code ?? "payment_capture_failed";
                    ctx.Saga.FailureReason = ctx.Message?.Error?.ToString();
                })
                .TransitionTo(Failed)
                .Finalize()
        );


        SetCompletedWhenFinalized();
    }
}