using System.Collections.Concurrent;

using PaymentGateway.Api.Application.Abstractions.CQRS;
using PaymentGateway.Api.Application.Abstractions.Persistence;
using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.Mappers;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.AcquiringBank;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Fraud;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Idempotency;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.PaymentValidation;
using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment;

public sealed class ProcessPaymentHandler(
    IPaymentRepository paymentsRepository,
    IUnitOfWork unitOfWork,
    IPaymentValidationHandler paymentValidationHandler,
    IIdempotencyCheckHandler idempotencyCheckHandler,
    IIdempotencyUpdateHandler idempotencyUpdateHandler,
    IFraudCheckHandler fraudCheckHandler,
    IAcquiringBankAuthorizeHandler acquiringBankAuthorizeHandler,
    ILogger<ProcessPaymentHandler> logger)
    : ICommandHandler<ProcessPaymentCommand, PaymentOperationResultDto>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> IdempotencyLocks = new(StringComparer.Ordinal);

    public async Task<PaymentOperationResultDto> HandleAsync(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var context = new ProcessPaymentExecutionContext(command);

        await ValidatePaymentRequest(context, cancellationToken);

        if (!context.CanContinue)
            return context.Result;

        context.CreatePayment();

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            context.Payment!.MarkAsIdempotencyVerified();

            await ExecuteNewPaymentAsync(context, cancellationToken);
            return context.Result;
        }

        var gate = IdempotencyLocks.GetOrAdd(
            command.IdempotencyKey,
            _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);

        try
        {
            await ExecuteNewPaymentWithIdempotencyAsync(context, cancellationToken);
        }
        finally
        {
            gate.Release();
        }

        return context.Result;
    }

    private async Task ExecuteNewPaymentWithIdempotencyAsync(
        ProcessPaymentExecutionContext context,
        CancellationToken cancellationToken)
    {
        await CheckIdempotency(context, cancellationToken);

        if (!context.CanContinue)
        {
            if (context.Result.Error is not null)
            {
                context.Payment?.MarkAsFailed(
                        context.Result.Error!.Code,
                        context.Result.Error.Message);
            }

            return;
        }

        context.Payment!.MarkAsIdempotencyVerified();

        await ExecuteNewPaymentAsync(context, cancellationToken);
    }

    private async Task ExecuteNewPaymentAsync(
        ProcessPaymentExecutionContext context,
        CancellationToken cancellationToken)
    {
        await RunFraudCheckAsync(context, cancellationToken);

        if (!context.CanContinue)
        {
            context.Payment?.MarkAsFailed(
                    context.Result.Error!.Code,
                    context.Result.Error.Message);
            return;
        }

        context.Payment!.MarkAsFraudCheckPassed();

        await RunAcquiringBankCheckAsync(context, cancellationToken);

        if (!context.CanContinue)
        {
            context.Payment?.MarkAsFailed(
                    context.Result.Error!.Code,
                    context.Result.Error.Message);
            return;
        }

        await CaptureAndPersistPayment(context, cancellationToken);
    }

    private async Task ValidatePaymentRequest(ProcessPaymentExecutionContext context, CancellationToken cancellationToken)
    {
        var validationErrors = await paymentValidationHandler.HandleAsync(context.Command, cancellationToken);

        if (validationErrors.Count == 0)
            return;

        logger.LogInformation(
            "Payment rejected due to validation errors: {ValidationErrors}",
            string.Join(',', validationErrors.Keys));

        context.StopExecution(
            PaymentOperationOutcome.BadRequest,
            PaymentFailureFactory.InvalidPaymentRequest(validationErrors));
    }

    private async Task CheckIdempotency(ProcessPaymentExecutionContext context, CancellationToken cancellationToken)
    {
        var idempotencyKey = context.Command.IdempotencyKey!;

        var idempotencyResult = await idempotencyCheckHandler.HandleAsync(
            new IdempotencyCheckCommand(idempotencyKey, context.RequestHash),
            cancellationToken);

        switch (idempotencyResult.Status)
        {
            case IdempotencyStatus.Conflict:
                context.StopExecution(
                    PaymentOperationOutcome.Conflict,
                    PaymentFailureFactory.IdempotencyConflict(idempotencyKey));
                return;

            case IdempotencyStatus.Duplicate when idempotencyResult.Payment is not null:
                context.StopExecution(new PaymentOperationResultDto
                {
                    Outcome = PaymentOperationOutcome.Ok,
                    Payment = idempotencyResult.Payment
                });
                return;

            case IdempotencyStatus.Duplicate:
                context.ContinueExecution(PaymentOperationOutcome.Ok);
                return;

            case IdempotencyStatus.Error:
                context.StopExecution(
                    PaymentOperationOutcome.BadRequest,
                    PaymentFailureFactory.IdempotencyError(
                        idempotencyKey,
                        idempotencyResult.Error?.Errors));
                return;

            case IdempotencyStatus.Updated when idempotencyResult.Payment is not null:
                context.ContinueExecution(
                    PaymentOperationOutcome.Ok,
                    idempotencyResult.Payment);
                return;

            default:
                context.ContinueExecution(PaymentOperationOutcome.Ok);
                return;
        }
    }

    private async Task RunFraudCheckAsync(
        ProcessPaymentExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await fraudCheckHandler.HandleAsync(
                new FraudCheckCommand(context.Command.CardNumber!),
                cancellationToken);

            if (response is null || !response.Authorized)
            {
                if (response is not null && !response.Authorized && response?.Error?.Code == "fraud_service_unavailable")
                {
                    context.StopExecution(
                        PaymentOperationOutcome.ServiceUnavailable,
                        PaymentFailureFactory.FraudServiceUnavailable());
                    return;
                }

                context.StopExecution(
                PaymentOperationOutcome.BadRequest,
                PaymentFailureFactory.PaymentDeclinedByFraudService());
                return;
            }

            logger.LogInformation(
                "Fraud check authorized payment for card ending {LastFourDigits}.",
                GetLastFourDigits(context.Command.CardNumber!));

            context.ContinueExecution(PaymentOperationOutcome.Ok);
        }
        catch (Exception ex) when (PaymentServiceExceptionHandler.IsServiceUnavailable(ex))
        {
            logger.LogError(
                ex,
                "Fraud service call failed. ExceptionType: {ExceptionType}",
                ex.GetType().FullName);

            context.StopExecution(
                PaymentOperationOutcome.ServiceUnavailable,
                PaymentFailureFactory.FraudServiceUnavailable());
        }
    }

    private async Task RunAcquiringBankCheckAsync(
        ProcessPaymentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var bankResponse = await acquiringBankAuthorizeHandler.HandleAsync(
            new AcquiringBankAuthorizeCommand(
                CardNumber: context.Command.CardNumber!,
                ExpiryMonth: context.Command.ExpiryMonth!.Value,
                ExpiryYear: context.Command.ExpiryYear!.Value,
                Currency: context.Command.Currency!,
                Amount: context.Command.Amount!.Value,
                Cvv: context.Command.Cvv!),
            cancellationToken);

        if (!bankResponse.Authorized)
        {
            if (bankResponse.Error?.Code == "bank_unavailable")
            {
                context.StopExecution(
                    PaymentOperationOutcome.ServiceUnavailable,
                    bankResponse.Error!);
                return;
            }
            context.StopExecution(
                PaymentOperationOutcome.BadRequest,
                bankResponse.Error!);
            return;
        }

        context.Payment!.MarkAsAuthorized("acquiring_bank", bankResponse.AuthorizationCode!);

        context.ContinueExecution(PaymentOperationOutcome.Ok);
    }

    private async Task CaptureAndPersistPayment(ProcessPaymentExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            context.Payment!.MarkAsCaptured();

            await paymentsRepository.AddAsync(context.Payment, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            var paymentDto = context.Payment.ToDto();

            await UpdateIdempotencyStoreIfRequired(context, paymentDto, cancellationToken);

            logger.LogInformation(
                "Payment {PaymentId} processed with status {Status}.",
                context.Payment.Id,
                context.Payment.Status);

            context.ContinueExecution(
                PaymentOperationOutcome.Created,
                paymentDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
            "Payment {PaymentId} failed with status {Status}.",
            context.Payment!.Id,
            context.Payment.Status);

            context.StopExecution(
                PaymentOperationOutcome.ServiceUnavailable,
                PaymentFailureFactory.PaymentPersistenceFailed());

            return;
        }
    }

    private async Task UpdateIdempotencyStoreIfRequired(
        ProcessPaymentExecutionContext context,
        PaymentDto paymentDto,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = context.Command.IdempotencyKey;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return;

        var result = await idempotencyUpdateHandler.HandleAsync(new IdempotencyUpdateCommand(
                context.Command.IdempotencyKey!,
                context.RequestHash,
                paymentDto),
            cancellationToken);

        if (result.Status == IdempotencyStatus.Updated)
            return;

        logger.LogWarning(
            "Payment {PaymentId} processed with status {Status} but could not be updated in idempotency service with key {IdempotencyKey}.",
            context.Payment!.Id,
            context.Payment.Status,
            idempotencyKey);
    }



    private static string GetLastFourDigits(string cardNumber)
    {
        return string.IsNullOrWhiteSpace(cardNumber)
            ? string.Empty
            : cardNumber.Length <= 4
            ? cardNumber
            : cardNumber[^4..];
    }
}