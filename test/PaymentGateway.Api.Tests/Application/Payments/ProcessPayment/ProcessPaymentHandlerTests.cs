using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Application.Common;
using PaymentGateway.Api.Application.Payments.Dtos;
using PaymentGateway.Api.Application.Payments.ProcessPayment;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

using System.Net;

namespace PaymenGateway.Api.Tests.Application.Payments.ProcessPayment;

public class ProcessPaymentHandlerTests
{
    private readonly ProcessPaymentHandler _handler;
    private readonly Mock<IPaymentsRepository> _paymentsRepositoryMock = new();
    private readonly Mock<IPaymentRequestValidator> _validatorMock = new();
    private readonly Mock<IAcquiringBankClient> _acquiringBankClientMock = new();
    private readonly Mock<ILogger<ProcessPaymentHandler>> _loggerMock = new();

    public ProcessPaymentHandlerTests()
    {
        _handler = new ProcessPaymentHandler(
            _paymentsRepositoryMock.Object,
            _validatorMock.Object,
            _acquiringBankClientMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void Handle_WhenValidationFails_ReturnsBadRequest()
    {
        // Arrange
        var command = new ProcessPaymentCommand(
            CardNumber: "4111111111111111",
            ExpiryMonth: 12,
            ExpiryYear: 2030,
            Cvv: "123",
            Amount: 100,
            Currency: "gbp",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B");

        var validationErrors = new Dictionary<string, string[]>
        {
            ["CardNumber"] = ["Card number is required."]
        };

        _validatorMock
            .Setup(x => x.Validate(command))
            .Returns(validationErrors);


        // Act
        var result = _handler.Handle(command);

        // Assert
        Assert.Equal(PaymentOperationOutcome.BadRequest, result.Outcome);
        Assert.NotNull(result.Error);
        Assert.Equal("payment_rejected", result.Error.Code);

        _acquiringBankClientMock.Verify(
            x => x.ProcessAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _paymentsRepositoryMock.Verify(
            x => x.TryAdd(
                It.IsAny<PostPaymentResponse>(),
                It.IsAny<string?>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void Handle_WhenBankAuthorizesPayment_ReturnsCreatedAndStoresPayment()
    {
        // Arrange
        var command = new ProcessPaymentCommand(
            CardNumber: "4111111111111111",
            ExpiryMonth: 12,
            ExpiryYear: 2030,
            Cvv: "123",
            Amount: 100,
            Currency: "gbp",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B");

        _validatorMock
            .Setup(x => x.Validate(command))
            .Returns(new Dictionary<string, string[]>());

        _acquiringBankClientMock
            .Setup(x => x.ProcessAsync(
                It.IsAny<BankPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankPaymentResponse(Authorized: true, AuthorizationCode: "authorization-code"));

        _paymentsRepositoryMock
            .Setup(x => x.TryAdd(
                It.IsAny<PostPaymentResponse>(),
                command.IdempotencyKey,
                It.IsAny<string>()))
            .Returns(true);

        // Act
        var result = _handler.Handle(command);

        // Assert
        Assert.Equal(PaymentOperationOutcome.Created, result.Outcome);
        Assert.NotNull(result.Payment);

        var payment = Assert.IsType<PostPaymentResponse>(result.Payment);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal(1111, payment.CardNumberLastFour);
        Assert.Equal(command.Amount, payment.Amount);
        Assert.Equal("GBP", payment.Currency);

        _paymentsRepositoryMock.Verify(
            x => x.TryAdd(
                It.Is<PostPaymentResponse>(p =>
                    p.Status == PaymentStatus.Authorized &&
                    p.CardNumberLastFour == 1111 &&
                    p.Currency == "GBP"),
                command.IdempotencyKey,
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void Handle_WhenBankDeclinesPayment_ReturnsCreatedWithDeclinedStatus()
    {
        // Arrange
        var command = new ProcessPaymentCommand(
           CardNumber: "4111111111111111",
           ExpiryMonth: 12,
           ExpiryYear: 2030,
           Cvv: "123",
           Amount: 100,
           Currency: "gbp",
           IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B");

        _validatorMock
            .Setup(x => x.Validate(command))
            .Returns(new Dictionary<string, string[]>());

        _acquiringBankClientMock
            .Setup(x => x.ProcessAsync(
                It.IsAny<BankPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankPaymentResponse(Authorized: false, AuthorizationCode: null));

        _paymentsRepositoryMock
            .Setup(x => x.TryAdd(
                It.IsAny<PostPaymentResponse>(),
                command.IdempotencyKey,
                It.IsAny<string>()))
            .Returns(true);

        // Act
        var result = _handler.Handle(command);

        // Assert
        Assert.Equal(PaymentOperationOutcome.BadRequest, result.Outcome);

        var payment = Assert.IsType<PostPaymentResponse>(result.Payment);
        Assert.Equal(PaymentStatus.Declined, payment.Status);
    }

    [Fact]
    public void Handle_WhenBankReturnsNull_ReturnsBadRequest()
    {
        // Arrange
        var command = new ProcessPaymentCommand(
           CardNumber: "4111111111111111",
           ExpiryMonth: 12,
           ExpiryYear: 2030,
           Cvv: "123",
           Amount: 100,
           Currency: "gbp",
           IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B");

        _validatorMock
            .Setup(x => x.Validate(command))
            .Returns(new Dictionary<string, string[]>());

        _acquiringBankClientMock
            .Setup(x => x.ProcessAsync(
                It.IsAny<BankPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BankPaymentResponse?)null);

        // Act
        var result = _handler.Handle(command);

        // Assert
        Assert.Equal(PaymentOperationOutcome.BadRequest, result.Outcome);
        Assert.NotNull(result.Error);
        Assert.Equal("payment_rejected", result.Error.Code);

        _paymentsRepositoryMock.Verify(
            x => x.TryAdd(
                It.IsAny<PostPaymentResponse>(),
                It.IsAny<string?>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void Handle_WhenBankUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        var command = new ProcessPaymentCommand(
           CardNumber: "4111111111111111",
           ExpiryMonth: 12,
           ExpiryYear: 2030,
           Cvv: "123",
           Amount: 100,
           Currency: "gbp",
           IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B");

        _validatorMock
            .Setup(x => x.Validate(command))
            .Returns(new Dictionary<string, string[]>());

        _acquiringBankClientMock
            .Setup(x => x.ProcessAsync(
                It.IsAny<BankPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(
                "Bank unavailable",
                null,
                HttpStatusCode.ServiceUnavailable));

        // Act
        var result = _handler.Handle(command);

        // Assert
        Assert.Equal(PaymentOperationOutcome.ServiceUnavailable, result.Outcome);
        Assert.NotNull(result.Error);
        Assert.Equal("bank_unavailable", result.Error.Code);

        _paymentsRepositoryMock.Verify(
            x => x.TryAdd(
                It.IsAny<PostPaymentResponse>(),
                It.IsAny<string?>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void Handle_WhenRepositoryTryAddFails_ReturnsConflict()
    {
        // Arrange
        var command = new ProcessPaymentCommand(
           CardNumber: "4111111111111111",
           ExpiryMonth: 12,
           ExpiryYear: 2030,
           Cvv: "123",
           Amount: 100,
           Currency: "gbp",
           IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B");

        _validatorMock
            .Setup(x => x.Validate(command))
            .Returns(new Dictionary<string, string[]>());

        _acquiringBankClientMock
            .Setup(x => x.ProcessAsync(
                It.IsAny<BankPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankPaymentResponse(Authorized: true, AuthorizationCode: "authorization-code"));

        _paymentsRepositoryMock
            .Setup(x => x.TryAdd(
                It.IsAny<PostPaymentResponse>(),
                command.IdempotencyKey,
                It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = _handler.Handle(command);

        // Assert
        Assert.Equal(PaymentOperationOutcome.Conflict, result.Outcome);
        Assert.NotNull(result.Error);
        Assert.Equal("payment_conflict", result.Error.Code);
    }

    [Fact]
    public void Handle_WhenSameIdempotencyKeyAndSameRequestExists_ReturnsOkWithExistingPayment()
    {
        // Arrange
        var command = new ProcessPaymentCommand(
           CardNumber: "4111111111111111",
           ExpiryMonth: 12,
           ExpiryYear: 2030,
           Cvv: "123",
           Amount: 100,
           Currency: "gbp",
           IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B");
        var requestHash = PaymentRequestHasher.Hash(command);

        var existingPayment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = 1111,
            ExpiryMonth = command.ExpiryMonth!.Value,
            ExpiryYear = command.ExpiryYear!.Value,
            Currency = command.Currency.ToUpperInvariant(),
            Amount = command.Amount!.Value
        };

        var existingRecord = new IdempotencyResult(
            Payment: existingPayment,
            RequestHash: requestHash);

        _validatorMock
            .Setup(x => x.Validate(command))
            .Returns(new Dictionary<string, string[]>());

        _paymentsRepositoryMock
            .Setup(x => x.TryGetByIdempotencyKey(command.IdempotencyKey!))
            .Returns(existingRecord);

        // Act
        var result = _handler.Handle(command);

        // Assert
        Assert.Equal(PaymentOperationOutcome.Ok, result.Outcome);
        Assert.Equal(existingPayment, result.Payment);

        _acquiringBankClientMock.Verify(
            x => x.ProcessAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Handle_WhenSameIdempotencyKeyButDifferentRequestExists_ReturnsConflict()
    {
        // Arrange
        var command = new ProcessPaymentCommand(
           CardNumber: "4111111111111111",
           ExpiryMonth: 12,
           ExpiryYear: 2030,
           Cvv: "123",
           Amount: 100,
           Currency: "gbp",
           IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B");

        var existingPayment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = 1111,
            ExpiryMonth = command.ExpiryMonth!.Value,
            ExpiryYear = command.ExpiryYear!.Value,
            Currency = command.Currency.ToUpperInvariant(),
            Amount = command.Amount!.Value
        };

        var existingRecord = new IdempotencyResult(
            Payment: existingPayment,
            RequestHash: "A15FFF44-7D05-417C-8E29-7DBE7F3BFBA8");

        _validatorMock
            .Setup(x => x.Validate(command))
            .Returns(new Dictionary<string, string[]>());

        _paymentsRepositoryMock
            .Setup(x => x.TryGetByIdempotencyKey(command.IdempotencyKey!))
            .Returns(existingRecord);

        // Act
        var result = _handler.Handle(command);

        // Assert
        Assert.Equal(PaymentOperationOutcome.Conflict, result.Outcome);
        Assert.NotNull(result.Error);
        Assert.Equal("idempotency_conflict", result.Error.Code);

        _acquiringBankClientMock.Verify(
            x => x.ProcessAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
