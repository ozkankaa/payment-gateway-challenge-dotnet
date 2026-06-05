using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Application.Abstractions.Persistence;
using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.AcquiringBank;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Fraud;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Idempotency;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.PaymentValidation;
using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;

namespace PaymentGateway.Api.Tests.Application.Features.Payments.ProcessPayment;

public sealed class ProcessPaymentHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPaymentValidationHandler> _validation = new();
    private readonly Mock<IIdempotencyCheckHandler> _idempotencyCheck = new();
    private readonly Mock<IIdempotencyUpdateHandler> _idempotencyUpdate = new();
    private readonly Mock<IFraudCheckHandler> _fraud = new();
    private readonly Mock<IAcquiringBankAuthorizeHandler> _bank = new();
    private readonly Mock<ILogger<ProcessPaymentHandler>> _logger = new();

    private ProcessPaymentHandler Sut() =>
        new(
            _paymentsRepository.Object,
            _unitOfWork.Object,
            _validation.Object,
            _idempotencyCheck.Object,
            _idempotencyUpdate.Object,
            _fraud.Object,
            _bank.Object,
            _logger.Object);

    [Fact]
    public async Task HandleAsync_WhenValidationFails_ReturnsBadRequest_AndStopsPipeline()
    {
        var command = ValidCommand();

        _validation
            .Setup(x => x.HandleAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>
            {
                ["cardNumber"] = ["Card number is required"]
            });

        var result = await Sut().HandleAsync(command, CancellationToken.None);

        result.Outcome.Should().Be(PaymentOperationOutcome.BadRequest);
        result.Error.Should().NotBeNull();

        _fraud.Verify(x => x.HandleAsync(It.IsAny<FraudCheckCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _bank.Verify(x => x.HandleAsync(It.IsAny<AcquiringBankAuthorizeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _paymentsRepository.Verify(x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenNoIdempotencyKey_AndPaymentSucceeds_ReturnsCreated_AndDoesNotCallIdempotencyServices()
    {
        var command = ValidCommand(idempotencyKey: null);
        SetupValidPaymentFlow();

        var result = await Sut().HandleAsync(command, CancellationToken.None);

        result.Outcome.Should().Be(PaymentOperationOutcome.Created);
        result.Payment.Should().NotBeNull();

        _idempotencyCheck.Verify(
            x => x.HandleAsync(It.IsAny<IdempotencyCheckCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _idempotencyUpdate.Verify(
            x => x.HandleAsync(It.IsAny<IdempotencyUpdateCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _paymentsRepository.Verify(x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenIdempotencyConflict_ReturnsConflict_AndDoesNotRunFraudOrBank()
    {
        var command = ValidCommand(idempotencyKey: "idem-123");

        _validation.Setup(x => x.HandleAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>());

        _idempotencyCheck
            .Setup(x => x.HandleAsync(It.IsAny<IdempotencyCheckCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyResult(Status: IdempotencyStatus.Conflict));

        var result = await Sut().HandleAsync(command, CancellationToken.None);

        result.Outcome.Should().Be(PaymentOperationOutcome.Conflict);
        result.Error.Should().NotBeNull();

        _fraud.Verify(x => x.HandleAsync(It.IsAny<FraudCheckCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _bank.Verify(x => x.HandleAsync(It.IsAny<AcquiringBankAuthorizeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _paymentsRepository.Verify(x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateWithStoredPayment_ReturnsOkWithExistingPayment_AndDoesNotCreateNewPayment()
    {
        var command = ValidCommand(idempotencyKey: "idem-123");
        var existingPayment = new PaymentDto { Id = Guid.NewGuid() };

        _validation.Setup(x => x.HandleAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>());

        _idempotencyCheck
            .Setup(x => x.HandleAsync(It.IsAny<IdempotencyCheckCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyResult(
                IdempotencyStatus.Duplicate,
                existingPayment,
                 RequestHash: "hash123"));

        var result = await Sut().HandleAsync(command, CancellationToken.None);

        result.Outcome.Should().Be(PaymentOperationOutcome.Ok);
        result.Payment.Should().Be(existingPayment);

        _fraud.Verify(x => x.HandleAsync(It.IsAny<FraudCheckCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _paymentsRepository.Verify(x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenFraudDeclines_ReturnsBadRequest_AndDoesNotCallBankOrPersist()
    {
        var command = ValidCommand();

        _validation.Setup(x => x.HandleAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>());

        _fraud
            .Setup(x => x.HandleAsync(It.IsAny<FraudCheckCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FraudCheckResult() { Authorized= false});

        var result = await Sut().HandleAsync(command, CancellationToken.None);

        result.Outcome.Should().Be(PaymentOperationOutcome.BadRequest);
        result.Error.Should().NotBeNull();

        _bank.Verify(x => x.HandleAsync(It.IsAny<AcquiringBankAuthorizeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _paymentsRepository.Verify(x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenFraudServiceUnavailable_ReturnsServiceUnavailable()
    {
        var command = ValidCommand();

        _validation.Setup(x => x.HandleAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>());

        _fraud
            .Setup(x => x.HandleAsync(It.IsAny<FraudCheckCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FraudCheckResult() { Authorized = false, Error = new ErrorDto(Code : "fraud_service_unavailable", Message : "Fraud service is unavailable. Try again later.") });

        var result = await Sut().HandleAsync(command, CancellationToken.None);

        result.Outcome.Should().Be(PaymentOperationOutcome.ServiceUnavailable);
        result.Error.Should().NotBeNull();

        _bank.Verify(x => x.HandleAsync(It.IsAny<AcquiringBankAuthorizeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _paymentsRepository.Verify(x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenBankDeclines_ReturnsBadRequest_AndDoesNotPersist()
    {
        var command = ValidCommand();

        _validation.Setup(x => x.HandleAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>());

        _fraud
            .Setup(x => x.HandleAsync(It.IsAny<FraudCheckCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FraudCheckResult() { Authorized = true });

        _bank
            .Setup(x => x.HandleAsync(It.IsAny<AcquiringBankAuthorizeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcquiringBankAuthorizeResult
            {
                Authorized = false,
                Error = new ErrorDto(Code : "card_declined",Message : "Card declined")
            });

        var result = await Sut().HandleAsync(command, CancellationToken.None);

        result.Outcome.Should().Be(PaymentOperationOutcome.BadRequest);
        result.Error!.Code.Should().Be("card_declined");

        _paymentsRepository.Verify(x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenBankUnavailable_ReturnsServiceUnavailable()
    {
        var command = ValidCommand();

        _validation.Setup(x => x.HandleAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>());

        _fraud
            .Setup(x => x.HandleAsync(It.IsAny<FraudCheckCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FraudCheckResult() { Authorized=true});

        _bank
            .Setup(x => x.HandleAsync(It.IsAny<AcquiringBankAuthorizeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcquiringBankAuthorizeResult
            {
                Authorized = false,
                Error = new ErrorDto(
                    Code : "bank_unavailable",
                    Message : "Bank unavailable"
                )
            });

        var result = await Sut().HandleAsync(command, CancellationToken.None);

        result.Outcome.Should().Be(PaymentOperationOutcome.ServiceUnavailable);
        result.Error!.Code.Should().Be("bank_unavailable");

        _paymentsRepository.Verify(x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceFails_ReturnsServiceUnavailable()
    {
        var command = ValidCommand();
        SetupValidPaymentFlow();

        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db failure"));

        var result = await Sut().HandleAsync(command, CancellationToken.None);

        result.Outcome.Should().Be(PaymentOperationOutcome.ServiceUnavailable);
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenIdempotencyKeyExists_AndPaymentSucceeds_UpdatesIdempotencyStore()
    {
        var command = ValidCommand(idempotencyKey: "idem-123");

        SetupValidPaymentFlow();

        _idempotencyCheck
            .Setup(x => x.HandleAsync(It.IsAny<IdempotencyCheckCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyResult(Status : IdempotencyStatus.Updated));

        _idempotencyUpdate
            .Setup(x => x.HandleAsync(It.IsAny<IdempotencyUpdateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyResult(Status : IdempotencyStatus.Updated));

        var result = await Sut().HandleAsync(command, CancellationToken.None);

        result.Outcome.Should().Be(PaymentOperationOutcome.Created);

        _idempotencyUpdate.Verify(
            x => x.HandleAsync(
                It.Is<IdempotencyUpdateCommand>(c => c.IdempotencyKey == "idem-123"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void SetupValidPaymentFlow()
    {
        _validation
            .Setup(x => x.HandleAsync(It.IsAny<ProcessPaymentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>());

        _fraud
            .Setup(x => x.HandleAsync(It.IsAny<FraudCheckCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FraudCheckResult { Authorized = true, AuthorizationCode = Guid.NewGuid().ToString() });

        _bank
            .Setup(x => x.HandleAsync(It.IsAny<AcquiringBankAuthorizeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcquiringBankAuthorizeResult
            {
                Authorized = true,
                AuthorizationCode = "auth_123"
            });

        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private static ProcessPaymentCommand ValidCommand(string? idempotencyKey = null)
    {
        return new ProcessPaymentCommand(
            MerchantId: Guid.NewGuid(),
            CardNumber: "4242424242424242",
            ExpiryMonth: 12,
            ExpiryYear: 2030,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: idempotencyKey);
    }
}