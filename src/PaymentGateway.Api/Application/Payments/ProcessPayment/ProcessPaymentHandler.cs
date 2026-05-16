using PaymentGateway.Api.Application.Abstractions;
using PaymentGateway.Api.Application.Common;
using PaymentGateway.Api.Application.Payments.Dtos;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

using System.Collections.Concurrent;
using System.Net;

namespace PaymentGateway.Api.Application.Payments.ProcessPayment;

public class ProcessPaymentHandler(IPaymentsRepository paymentsRepository,
    IPaymentRequestValidator validator,
    IAcquiringBankClient acquiringBankClient,
    ILogger<ProcessPaymentHandler> logger) : ICommandHandler<ProcessPaymentCommand, PaymentOperationResult>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> IdempotencyLocks = new(StringComparer.Ordinal);

    public PaymentOperationResult Handle(ProcessPaymentCommand command)
    {
        var validationErrors = validator.Validate(command);

        if (validationErrors.Count > 0)
        {
            logger.LogInformation("Payment rejected due to validation errors: {Fields}", string.Join(',', validationErrors.Keys));
            return new PaymentOperationResult(PaymentOperationOutcome.BadRequest, null, Error: new ErrorResponse("payment_rejected", "Invalid payment request.", validationErrors));
        }

        var requestHash = PaymentRequestHasher.Hash(command);

        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var gate = IdempotencyLocks.GetOrAdd(command.IdempotencyKey, _ => new SemaphoreSlim(1, 1));
            gate.Wait();
            try
            {
                return ProcessWithIdempotencyLockAsync(command, requestHash);
            }
            finally
            {
                gate.Release();
            }
        }

        return ProcessNewPaymentAsync(command, requestHash);
    }

    private PaymentOperationResult ProcessWithIdempotencyLockAsync(ProcessPaymentCommand command, string requestHash)
    {
        var existing = paymentsRepository.TryGetByIdempotencyKey(command.IdempotencyKey!);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return new PaymentOperationResult(
                    PaymentOperationOutcome.Conflict,
                    Error: new ErrorResponse("idempotency_conflict", "The Idempotency-Key was already used with a different request body."));
            }

            return new PaymentOperationResult(PaymentOperationOutcome.Ok, existing.Payment);
        }

        return ProcessNewPaymentAsync(command, requestHash);
    }

    private PaymentOperationResult ProcessNewPaymentAsync(ProcessPaymentCommand command, string requestHash)
    {
        BankPaymentResponse? bankResponse;
        try
        {
            bankResponse = acquiringBankClient.ProcessAsync(new BankPaymentRequest(
                CardNumber: command.CardNumber,
                ExpiryDate: $"{command.ExpiryMonth:00}/{command.ExpiryYear}",
                Cvv: command.Cvv,
                Amount: command.Amount!.Value,
                Currency: command.Currency.ToUpperInvariant()
                ), CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return new PaymentOperationResult(
                PaymentOperationOutcome.ServiceUnavailable,
                Error: new ErrorResponse("bank_unavailable", "Acquiring bank is unavailable. Try again later."));
        }

        if (bankResponse is null)
        {
            return new PaymentOperationResult(
                PaymentOperationOutcome.BadRequest,
                Error: new ErrorResponse("payment_rejected", "Acquiring bank rejected the payment request."));
        }

        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = int.Parse(command.CardNumber[^4..]),
            ExpiryMonth = command.ExpiryMonth!.Value,
            ExpiryYear = command.ExpiryYear!.Value,
            Currency = command.Currency.ToUpperInvariant(),
            Amount = command.Amount!.Value
        };

        if (!bankResponse.Authorized)
        {
            return new PaymentOperationResult(
                PaymentOperationOutcome.BadRequest,
                Payment: payment,
                Error: new ErrorResponse("payment_declined", "Acquiring bank declined the payment request."));
        }

        if (!paymentsRepository.TryAdd(payment, command.IdempotencyKey, requestHash))
        {
            return new PaymentOperationResult(
                PaymentOperationOutcome.Conflict,
                Error: new ErrorResponse("payment_conflict", "Payment could not be stored consistently. Retry with the same Idempotency-Key."));
        }

        logger.LogInformation("Payment {PaymentId} processed with status {Status}", payment.Id, payment.Status);
        return new PaymentOperationResult(PaymentOperationOutcome.Created, payment);
    }
}
