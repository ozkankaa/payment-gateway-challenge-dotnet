using MassTransit;

using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Saga.Request.Messages;

namespace PaymentGateway.Api.Saga.Request;

public class PaymentRequestStateMachine : MassTransitStateMachine<PaymentRequestState>
{
    public Event<StartPaymentRequest> StartPayment { get; private set; } = default!;

    public Request<PaymentRequestState, ValidatePaymentRequest, PaymentValidatedResponse, PaymentValidationFailedResponse> Validation { get; private set; } = default!;
    public Request<PaymentRequestState, CheckIdempotencyRequest, IdempotencyAcceptedResponse, DuplicatePaymentDetectedResponse, IdempotencyFailedResponse> Idempotency { get; private set; } = default!;
    public Request<PaymentRequestState, CheckFraudRequest, FraudApprovedResponse, FraudRejectedResponse, FraudFailedResponse> Fraud { get; private set; } = default!;
    public Request<PaymentRequestState, AuthorizePaymentRequest, PaymentAuthorizedResponse, PaymentDeclinedResponse, PaymentAuthorizationFailedResponse> Authorize { get; private set; } = default!;
    public Request<PaymentRequestState, CapturePaymentRequest, PaymentCapturedResponse, PaymentCaptureFailedResponse> Capture { get; private set; } = default!;
    public PaymentRequestStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Request(() => Validation, r => r.Timeout = TimeSpan.Zero);
        Request(() => Idempotency, r => r.Timeout = TimeSpan.Zero);
        Request(() => Fraud, r => r.Timeout = TimeSpan.Zero);
        Request(() => Authorize, r => r.Timeout = TimeSpan.Zero);
        Request(() => Capture, r => r.Timeout = TimeSpan.Zero);

        Initially(
            When(StartPayment)
                .Then(ctx =>
                {
                    ctx.Saga.OriginalRequestId = ctx.RequestId;
                    ctx.Saga.OriginalResponseAddress = ctx.ResponseAddress;

                    ctx.Saga.CorrelationId = ctx.Message.CorrelationId;
                    ctx.Saga.PaymentId = ctx.Message.PaymentId;
                    ctx.Saga.MerchantId = ctx.Message.MerchantId;
                    ctx.Saga.CardToken = ctx.Message.CardToken;
                    ctx.Saga.CardLast4 = ctx.Message.CardLast4;
                    ctx.Saga.Amount = ctx.Message.Amount;
                    ctx.Saga.Currency = ctx.Message.Currency;
                    ctx.Saga.IdempotencyKey = ctx.Message.IdempotencyKey;
                    ctx.Saga.RequestHash = ctx.Message.RequestHash;
                    ctx.Saga.CreatedAt = DateTime.UtcNow;
                })
                .TransitionTo(Validation.Pending)
                .Request(Validation, ctx => new ValidatePaymentRequest(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.PaymentId,
                    ctx.Saga.MerchantId,
                    ctx.Message.CardNumber,
                    ctx.Message.ExpiryMonth,
                    ctx.Message.ExpiryYear,
                    ctx.Message.Currency,
                    ctx.Message.Amount,
                    ctx.Message.Cvv,
                    ctx.Saga.IdempotencyKey))
        );

        During(Validation.Pending,

            When(Validation.Completed)
                .Request(Idempotency, ctx => new CheckIdempotencyRequest(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.PaymentId,
                    ctx.Saga.IdempotencyKey,
                    ctx.Saga.RequestHash!,
                    ctx.Message.CardNumber,
                    ctx.Message.ExpiryMonth,
                    ctx.Message.ExpiryYear,
                    ctx.Message.Currency,
                    ctx.Message.Amount,
                    ctx.Message.Cvv))
                .TransitionTo(Idempotency.Pending),

            When(Validation.Completed2)
                .ThenAsync(ctx => SendFinalResponse(ctx, new PaymentFailedResponse(
                    ctx.Saga.PaymentId,
                    ctx.Message.Error)))
                .Finalize(),

            When(Validation.Faulted)
                .ThenAsync(ctx => SendFinalResponse(ctx, new PaymentFailedResponse(
                    ctx.Saga.PaymentId,
                    new ErrorDto("validation_failed", "Validation failed"))))
                .Finalize()
        );

        During(Idempotency.Pending,

            When(Idempotency.Completed)
                .Request(Fraud, ctx => new CheckFraudRequest(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.PaymentId,
                    ctx.Message.CardNumber,
                    ctx.Message.ExpiryMonth,
                    ctx.Message.ExpiryYear,
                    ctx.Message.Currency,
                    ctx.Message.Amount,
                    ctx.Message.Cvv))
                .TransitionTo(Fraud.Pending),

            When(Idempotency.Completed2)
                .ThenAsync(ctx => SendFinalResponse(ctx, new PaymentSucceededResponse(
                    ctx.Saga.PaymentId,
                    ctx.Message.Payment!)))
                .Finalize(),

            When(Idempotency.Completed3)
                .ThenAsync(ctx => SendFinalResponse(ctx, new PaymentFailedResponse(
                    ctx.Saga.PaymentId,
                    ctx.Message.Error)))
                .Finalize()
        );

        During(Fraud.Pending,

            When(Fraud.Completed)
                .Request(Authorize, ctx => new AuthorizePaymentRequest(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.PaymentId,
                    ctx.Message.CardNumber,
                    ctx.Message.ExpiryMonth,
                    ctx.Message.ExpiryYear,
                    ctx.Message.Cvv,
                    ctx.Message.Amount,
                    ctx.Message.Currency))
                .TransitionTo(Authorize.Pending),

            When(Fraud.Completed2)
                .ThenAsync(ctx => SendFinalResponse(ctx, new PaymentFailedResponse(
                    ctx.Saga.PaymentId,
                    ctx.Message.Error)))
                .Finalize(),

            When(Fraud.Completed3)
                .ThenAsync(ctx => SendFinalResponse(ctx, new PaymentFailedResponse(
                    ctx.Saga.PaymentId,
                    ctx.Message.Error)))
                .Finalize()
        );

        During(Authorize.Pending,

            When(Authorize.Completed)
                .Request(Capture, ctx => new CapturePaymentRequest(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.PaymentId,
                    ctx.Saga.MerchantId,
                    ctx.Message.PspId!,
                    ctx.Message.PspTransactionId!,
                    ctx.Message.CardNumber,
                    ctx.Message.ExpiryMonth,
                    ctx.Message.ExpiryYear,
                    ctx.Saga.Currency!,
                    ctx.Saga.Amount,
                    ctx.Saga.IdempotencyKey,
                    ctx.Saga.RequestHash!))
                .TransitionTo(Capture.Pending),

            When(Authorize.Completed2)
                .ThenAsync(ctx => SendFinalResponse(ctx, new PaymentFailedResponse(
                    ctx.Saga.PaymentId,
                    ctx.Message.Error)))
                .Finalize(),

            When(Authorize.Completed3)
                .ThenAsync(ctx => SendFinalResponse(ctx, new PaymentFailedResponse(
                    ctx.Saga.PaymentId,
                    ctx.Message.Error)))
                .Finalize()
        );

        During(Capture.Pending,

            When(Capture.Completed)
                .ThenAsync(ctx => SendFinalResponse(ctx, new PaymentSucceededResponse(
                    ctx.Saga.PaymentId,
                    ctx.Message.Payment!)))
                .Finalize(),

            When(Capture.Completed2)
                .ThenAsync(ctx => SendFinalResponse(ctx, new PaymentFailedResponse(
                    ctx.Saga.PaymentId,
                    ctx.Message.Error)))
                .Finalize(),

            When(Capture.Faulted)
                .ThenAsync(ctx => SendFinalResponse(ctx, new PaymentFailedResponse(
                    ctx.Saga.PaymentId,
                    new ErrorDto("capture_failed", "Capture payment failed"))))
                .Finalize()
        );

        SetCompletedWhenFinalized();
    }

    private static async Task SendFinalResponse<TData, TMessage>(
        BehaviorContext<PaymentRequestState, TData> ctx,
        TMessage message)
        where TData : class
        where TMessage : class
    {
        if (ctx.Saga.OriginalResponseAddress is null)
            throw new InvalidOperationException("OriginalResponseAddress is missing");

        if (ctx.Saga.OriginalRequestId is null)
            throw new InvalidOperationException("OriginalRequestId is missing");

        var endpoint = await ctx.GetSendEndpoint(ctx.Saga.OriginalResponseAddress);

        await endpoint.Send(message, sendCtx => sendCtx.RequestId = ctx.Saga.OriginalRequestId);
    }
}