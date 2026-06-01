using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.Mappers;
using PaymentGateway.Api.Domain.Entities.Payments;
using PaymentGateway.Api.Domain.Exceptions;

namespace PaymentGateway.Api.Application.Features.Payments.ProcessPayment;

public class ProcessPaymentExecutionContext
{
    public ProcessPaymentCommand Command { get; }

    public string IdempotencyKey { get; set; } = Guid.Empty.ToString();

    public string RequestHash { get; private set; } = string.Empty;

    public PaymentOperationResultDto Result { get; private set; }

    public Payment? Payment { get; private set; }

    public bool CanContinue { get; private set; } = true;

    public ProcessPaymentExecutionContext(ProcessPaymentCommand command)
    {
        Command = command;
        RequestHash = CreateRequestHash(command);
        Result = new PaymentOperationResultDto
        {
            Outcome = PaymentOperationOutcome.Ok
        };
    }

    private static string CreateRequestHash(ProcessPaymentCommand command)
    {
        var payload = JsonSerializer.Serialize(new
        {
            command.MerchantId,
            command.CardNumber,
            command.ExpiryMonth,
            command.ExpiryYear,
            command.Cvv,
            command.Amount,
            Currency = command.Currency.ToUpperInvariant()
        });

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(bytes);
    }

    public void CreatePayment()
    {
        var idempotencyKey = Command.IdempotencyKey ?? Guid.Empty.ToString();

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            StopExecution(
                PaymentOperationOutcome.BadRequest,
                PaymentFailureFactory.IdempotencyError(
                    string.Empty,
                    new Dictionary<string, string[]>
                    {
                        ["IdempotencyKey"] =
                        [
                            "Idempotency-Key header is required."
                        ]
                    }));

            return;
        }
        try
        {
            Payment = Payment.Create(
            idempotencyKey,
            Command.MerchantId,
            CardDetails.Create(
                Command.CardNumber[^4..],
                Command.ExpiryMonth!.Value,
                Command.ExpiryYear!.Value),
            Money.Create(
                Command.Amount!.Value,
                Command.Currency));
        }
        catch (DomainValidationException ex)
        {
            StopExecution(
                PaymentOperationOutcome.BadRequest,
                PaymentFailureFactory.IdempotencyError(
                    string.Empty,
                    new Dictionary<string, string[]>
                    {
                        [ex.PropertyName ?? "Payment"] =
                        [
                            ex.Message
                        ]
                    }));

        }
    }

    public void StopExecution(
        PaymentOperationOutcome outcome,
        ErrorDto error)
    {
        CanContinue = false;

        Result = new PaymentOperationResultDto
        {
            Outcome = outcome,
            Error = error,
            Payment = Payment?.ToDto()
        };
    }

    public void StopExecution(PaymentOperationResultDto result)
    {
        CanContinue = false;
        Result = result;
    }

    public void ContinueExecution(
        PaymentOperationOutcome outcome,
        PaymentDto? payment = null)
    {
        Result = new PaymentOperationResultDto
        {
            Outcome = outcome,
            Payment = payment ?? Payment?.ToDto()
        };
    }
}
