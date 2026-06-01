using PaymentGateway.Api.Domain.Abstractions;
using PaymentGateway.Api.Domain.Events.PaymentAuthorized;
using PaymentGateway.Api.Domain.Events.PaymentCaptured;
using PaymentGateway.Api.Domain.Events.PaymentCreated;
using PaymentGateway.Api.Domain.Events.PaymentFailed;
using PaymentGateway.Api.Domain.Exceptions;

namespace PaymentGateway.Api.Domain.Entities.Payments;

public sealed class Payment : AggregateRoot, IEntity
{
    private Payment()
    {
    }

    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;

    public CardDetails CardDetails { get; private set; } = null!;
    public Money Money { get; private set; } = null!;

    public ProviderReference? ProviderReference { get; private set; }

    public string FailureCode { get; private set; } = string.Empty;
    public string FailureMessage { get; private set; } = string.Empty;

    public PaymentStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static Payment Create(
        string idempotencyKey,
        Guid merchantId,
        CardDetails cardDetails,
        Money money)
    {
        if (merchantId == Guid.Empty)
            throw new DomainValidationException("Merchant id is required.", nameof(merchantId));

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainValidationException("Idempotency key is required.", nameof(idempotencyKey));

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            IdempotencyKey = idempotencyKey,
            CardDetails = cardDetails,
            Money = money,
            Status = PaymentStatus.Created,
            CreatedAtUtc = DateTime.UtcNow
        };

        payment.RaiseDomainEvent(new PaymentCreatedDomainEvent(
            payment.Id,
            payment.MerchantId,
            payment.IdempotencyKey,
            payment.Money.Amount,
            payment.Money.Currency));

        return payment;
    }

    public void MarkAsIdempotencyVerified()
    {
        EnsureStatus(PaymentStatus.Created);

        Status = PaymentStatus.IdempotencyVerified;
        Touch();
    }

    public void MarkAsFraudCheckPassed()
    {
        EnsureStatus(PaymentStatus.IdempotencyVerified);

        Status = PaymentStatus.FraudCheckPassed;
        Touch();
    }

    public void MarkAsAuthorized(string providerId, string providerToken)
    {
        EnsureStatus(PaymentStatus.FraudCheckPassed);

        ProviderReference = ProviderReference.Create(providerId, providerToken);
        Status = PaymentStatus.Authorized;
        Touch();

        RaiseDomainEvent(new PaymentAuthorizedDomainEvent(
            Id,
            MerchantId,
            providerId));
    }

    public void MarkAsCaptured()
    {
        EnsureStatus(PaymentStatus.Authorized);

        Status = PaymentStatus.Captured;
        Touch();

        RaiseDomainEvent(new PaymentCapturedDomainEvent(Id, MerchantId));
    }

    public void MarkAsFailed(string failureCode, string failureMessage)
    {
        if (Status is PaymentStatus.Captured)
            throw new InvalidOperationException("Captured payment cannot be failed.");

        if (string.IsNullOrWhiteSpace(failureCode))
            throw new ArgumentException("Failure code is required.", nameof(failureCode));

        if (string.IsNullOrWhiteSpace(failureMessage))
            throw new ArgumentException("Failure message is required.", nameof(failureMessage));

        FailureCode = failureCode;
        FailureMessage = failureMessage;
        Status = PaymentStatus.Failed;
        Touch();

        RaiseDomainEvent(new PaymentFailedDomainEvent(
            Id,
            MerchantId,
            failureCode,
            failureMessage));
    }

    private void EnsureStatus(PaymentStatus expectedStatus)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidOperationException(
                $"Payment must be {expectedStatus} but current status is {Status}.");
        }
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTime.UtcNow;
    }
}