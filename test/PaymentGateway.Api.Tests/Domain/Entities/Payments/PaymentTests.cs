using PaymentGateway.Api.Domain.Events.PaymentAuthorized;
using PaymentGateway.Api.Domain.Events.PaymentCaptured;
using PaymentGateway.Api.Domain.Events.PaymentCreated;
using PaymentGateway.Api.Domain.Events.PaymentFailed;
using PaymentGateway.Api.Domain.Exceptions;
using PaymentGateway.Api.Domain.Entities.Payments;

namespace PaymentGateway.Api.Tests.Domain.Entities.Payments;

public sealed class PaymentTests
{
    [Fact]
    public void Create_WithValidData_CreatesPayment()
    {
        var merchantId = Guid.NewGuid();
        var cardDetails = CreateCardDetails();
        var money = CreateMoney();

        var payment = Payment.Create(
            "idem-123",
            merchantId,
            cardDetails,
            money);

        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(merchantId, payment.MerchantId);
        Assert.Equal("idem-123", payment.IdempotencyKey);
        Assert.Equal(cardDetails, payment.CardDetails);
        Assert.Equal(money, payment.Money);
        Assert.Equal(PaymentStatus.Created, payment.Status);
        Assert.NotEqual(default, payment.CreatedAtUtc);
        Assert.Null(payment.UpdatedAtUtc);
    }

    [Fact]
    public void Create_RaisesPaymentCreatedDomainEvent()
    {
        var payment = CreatePayment();

        var domainEvent = Assert.Single(payment.DomainEvents);

        var createdEvent = Assert.IsType<PaymentCreatedDomainEvent>(domainEvent);
        Assert.Equal(payment.Id, createdEvent.PaymentId);
        Assert.Equal(payment.MerchantId, createdEvent.MerchantId);
        Assert.Equal(payment.IdempotencyKey, createdEvent.IdempotencyKey);
        Assert.Equal(payment.Money.Amount, createdEvent.Amount);
        Assert.Equal(payment.Money.Currency, createdEvent.Currency);
    }

    [Fact]
    public void Create_WithEmptyMerchantId_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Payment.Create(
                "idem-123",
                Guid.Empty,
                CreateCardDetails(),
                CreateMoney()));

        Assert.Contains("Merchant id is required", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidIdempotencyKey_ThrowsDomainValidationException(
        string? idempotencyKey)
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Payment.Create(
                idempotencyKey!,
                Guid.NewGuid(),
                CreateCardDetails(),
                CreateMoney()));

        Assert.Contains("Idempotency key is required", exception.Message);
    }

    [Fact]
    public void MarkAsIdempotencyVerified_WhenCreated_UpdatesStatus()
    {
        var payment = CreatePayment();

        payment.MarkAsIdempotencyVerified();

        Assert.Equal(PaymentStatus.IdempotencyVerified, payment.Status);
        Assert.NotNull(payment.UpdatedAtUtc);
    }

    [Fact]
    public void MarkAsIdempotencyVerified_WhenNotCreated_ThrowsInvalidOperationException()
    {
        var payment = CreatePayment();
        payment.MarkAsIdempotencyVerified();

        var exception = Assert.Throws<InvalidOperationException>(
            payment.MarkAsIdempotencyVerified);

        Assert.Contains("Payment must be Created", exception.Message);
    }

    [Fact]
    public void MarkAsFraudCheckPassed_WhenIdempotencyVerified_UpdatesStatus()
    {
        var payment = CreatePayment();
        payment.MarkAsIdempotencyVerified();

        payment.MarkAsFraudCheckPassed();

        Assert.Equal(PaymentStatus.FraudCheckPassed, payment.Status);
        Assert.NotNull(payment.UpdatedAtUtc);
    }

    [Fact]
    public void MarkAsFraudCheckPassed_WhenInvalidStatus_ThrowsInvalidOperationException()
    {
        var payment = CreatePayment();

        var exception = Assert.Throws<InvalidOperationException>(
            payment.MarkAsFraudCheckPassed);

        Assert.Contains("Payment must be IdempotencyVerified", exception.Message);
    }

    [Fact]
    public void MarkAsAuthorized_WhenFraudCheckPassed_UpdatesStatusAndProviderReference()
    {
        var payment = CreatePayment();
        payment.MarkAsIdempotencyVerified();
        payment.MarkAsFraudCheckPassed();

        payment.MarkAsAuthorized("provider-id", "provider-token");

        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.NotNull(payment.ProviderReference);
        Assert.NotNull(payment.UpdatedAtUtc);
    }

    [Fact]
    public void MarkAsAuthorized_RaisesPaymentAuthorizedDomainEvent()
    {
        var payment = CreatePayment();
        payment.MarkAsIdempotencyVerified();
        payment.MarkAsFraudCheckPassed();

        payment.MarkAsAuthorized("provider-id", "provider-token");

        var authorizedEvent = payment.DomainEvents
            .OfType<PaymentAuthorizedDomainEvent>()
            .Single();

        Assert.Equal(payment.Id, authorizedEvent.PaymentId);
        Assert.Equal(payment.MerchantId, authorizedEvent.MerchantId);
        Assert.Equal("provider-id", authorizedEvent.ProviderId);
    }

    [Fact]
    public void MarkAsAuthorized_WhenInvalidStatus_ThrowsInvalidOperationException()
    {
        var payment = CreatePayment();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            payment.MarkAsAuthorized("provider-id", "provider-token"));

        Assert.Contains("Payment must be FraudCheckPassed", exception.Message);
    }

    [Fact]
    public void MarkAsCaptured_WhenAuthorized_UpdatesStatus()
    {
        var payment = CreateAuthorizedPayment();

        payment.MarkAsCaptured();

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.NotNull(payment.UpdatedAtUtc);
    }

    [Fact]
    public void MarkAsCaptured_RaisesPaymentCapturedDomainEvent()
    {
        var payment = CreateAuthorizedPayment();

        payment.MarkAsCaptured();

        var capturedEvent = payment.DomainEvents
            .OfType<PaymentCapturedDomainEvent>()
            .Single();

        Assert.Equal(payment.Id, capturedEvent.PaymentId);
        Assert.Equal(payment.MerchantId, capturedEvent.MerchantId);
    }

    [Fact]
    public void MarkAsCaptured_WhenInvalidStatus_ThrowsInvalidOperationException()
    {
        var payment = CreatePayment();

        var exception = Assert.Throws<InvalidOperationException>(
            payment.MarkAsCaptured);

        Assert.Contains("Payment must be Authorized", exception.Message);
    }

    [Fact]
    public void MarkAsFailed_WhenNotCaptured_UpdatesFailureDetailsAndStatus()
    {
        var payment = CreatePayment();

        payment.MarkAsFailed("DECLINED", "Card was declined");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("DECLINED", payment.FailureCode);
        Assert.Equal("Card was declined", payment.FailureMessage);
        Assert.NotNull(payment.UpdatedAtUtc);
    }

    [Fact]
    public void MarkAsFailed_RaisesPaymentFailedDomainEvent()
    {
        var payment = CreatePayment();

        payment.MarkAsFailed("DECLINED", "Card was declined");

        var failedEvent = payment.DomainEvents
            .OfType<PaymentFailedDomainEvent>()
            .Single();

        Assert.Equal(payment.Id, failedEvent.PaymentId);
        Assert.Equal(payment.MerchantId, failedEvent.MerchantId);
        Assert.Equal("DECLINED", failedEvent.FailureCode);
        Assert.Equal("Card was declined", failedEvent.FailureMessage);
    }

    [Fact]
    public void MarkAsFailed_WhenCaptured_ThrowsInvalidOperationException()
    {
        var payment = CreateAuthorizedPayment();
        payment.MarkAsCaptured();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            payment.MarkAsFailed("ERROR", "Some error"));

        Assert.Contains("Captured payment cannot be failed", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void MarkAsFailed_WithInvalidFailureCode_ThrowsArgumentException(
        string? failureCode)
    {
        var payment = CreatePayment();

        Assert.Throws<ArgumentException>(() =>
            payment.MarkAsFailed(failureCode!, "Failure message"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void MarkAsFailed_WithInvalidFailureMessage_ThrowsArgumentException(
        string? failureMessage)
    {
        var payment = CreatePayment();

        Assert.Throws<ArgumentException>(() =>
            payment.MarkAsFailed("FAILURE_CODE", failureMessage!));
    }

    private static Payment CreatePayment()
    {
        return Payment.Create(
            "idem-123",
            Guid.NewGuid(),
            CreateCardDetails(),
            CreateMoney());
    }

    private static Payment CreateAuthorizedPayment()
    {
        var payment = CreatePayment();

        payment.MarkAsIdempotencyVerified();
        payment.MarkAsFraudCheckPassed();
        payment.MarkAsAuthorized("provider-id", "provider-token");

        return payment;
    }

    private static CardDetails CreateCardDetails()
    {
        return CardDetails.Create(
            "1234",
            12,
            2030);
    }

    private static Money CreateMoney()
    {
        return Money.Create(100, "GBP");
    }
}