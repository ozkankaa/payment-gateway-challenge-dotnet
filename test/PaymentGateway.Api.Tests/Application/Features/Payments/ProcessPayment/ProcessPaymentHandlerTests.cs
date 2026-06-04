//using Microsoft.Extensions.Logging;

//using Moq;

//using PaymentGateway.Api.Application.Abstractions.Persistence;
//using PaymentGateway.Api.Application.Features.Payments.Dtos;
//using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
//using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.PaymentValidation;
//using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService;
//using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Requests;
//using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Responses;
//using PaymentGateway.Api.Infrastructure.Services.FraudService;
//using PaymentGateway.Api.Infrastructure.Services.FraudService.Requests;
//using PaymentGateway.Api.Infrastructure.Services.FraudService.Responses;
//using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;

//namespace PaymentGateway.Api.Tests.Application.Features.Payments.ProcessPayment;

//public sealed class ProcessPaymentHandlerTests
//{
//    private readonly Mock<IPaymentRepository> _paymentsRepository = new();
//    private readonly Mock<IUnitOfWork> _unitOfWork = new();
//    private readonly Mock<IProcessPaymentCommandValidator> _validator = new();
//    private readonly Mock<IIdempotencyService> _idempotencyService = new();
//    private readonly Mock<IFraudServiceClient> _fraudServiceClient = new();
//    private readonly Mock<IAcquiringBankClient> _acquiringBankClient = new();
//    private readonly Mock<ILogger<ProcessPaymentHandler>> _logger = new();

//    private readonly ProcessPaymentHandler _processPaymentHandler;

//    public ProcessPaymentHandlerTests()
//    {
//        _processPaymentHandler = new ProcessPaymentHandler(
//            _paymentsRepository.Object,
//            _unitOfWork.Object,
//            _validator.Object,
//            _idempotencyService.Object,
//            _fraudServiceClient.Object,
//            _acquiringBankClient.Object,
//            _logger.Object);

//        _validator
//            .Setup(x => x.Validate(It.IsAny<ProcessPaymentCommand>()))
//            .Returns(new Dictionary<string, string[]>());

//        _idempotencyService
//            .Setup(x => x.TryAdd(It.IsAny<string>(), It.IsAny<string>()))
//            .Returns(new IdempotencyResult(IdempotencyStatus.Added));

//        _idempotencyService
//            .Setup(x => x.TryUpdate(
//                It.IsAny<PaymentDto>(),
//                It.IsAny<string>(),
//                It.IsAny<string>()))
//            .Returns(new IdempotencyResult(IdempotencyStatus.Updated));

//        _fraudServiceClient
//            .Setup(x => x.CheckAsync(
//                It.IsAny<FraudCheckRequest>(),
//                It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new FraudCheckResponse(Authorized: true, AuthorizationCode:Guid.NewGuid().ToString()));

//        _acquiringBankClient
//            .Setup(x => x.ProcessAsync(
//                It.IsAny<BankPaymentRequest>(),
//                It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new BankPaymentResponse(true, "AUTH-123"));

//        _unitOfWork
//            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
//            .ReturnsAsync(1);
//    }

//    [Fact]
//    public async Task HandleAsync_WhenCommandIsValid_ShouldCreatePayment()
//    {
//        var command = CreateValidCommand();

//        var result = await _processPaymentHandler.HandleAsync(command, CancellationToken.None);

//        Assert.Equal(PaymentOperationOutcome.Created, result.Outcome);
//        Assert.NotNull(result.Payment);
//        Assert.Null(result.Error);

//        _paymentsRepository.Verify(
//            x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()),
//            Times.Once);

//        _unitOfWork.Verify(
//            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
//            Times.Once);

//        _idempotencyService.Verify(
//            x => x.TryUpdate(It.IsAny<PaymentDto>(), command.IdempotencyKey!, It.IsAny<string>()),
//            Times.Once);
//    }

//    [Fact]
//    public async Task HandleAsync_WhenValidationFails_ShouldReturnBadRequestAndStop()
//    {
//        var command = CreateValidCommand();

//        _validator
//            .Setup(x => x.Validate(command))
//            .Returns(new Dictionary<string, string[]>
//            {
//                ["Amount"] = ["Amount is required."]
//            });

//        var result = await _processPaymentHandler.HandleAsync(command, CancellationToken.None);

//        Assert.Equal(PaymentOperationOutcome.BadRequest, result.Outcome);
//        Assert.Equal("payment_rejected", result.Error!.Code);

//        _fraudServiceClient.Verify(
//            x => x.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()),
//            Times.Never);

//        _paymentsRepository.Verify(
//            x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()),
//            Times.Never);
//    }

//    [Fact]
//    public async Task HandleAsync_WhenIdempotencyConflict_ShouldReturnConflict()
//    {
//        var command = CreateValidCommand();

//        _idempotencyService
//            .Setup(x => x.TryAdd(command.IdempotencyKey!, It.IsAny<string>()))
//            .Returns(new IdempotencyResult(IdempotencyStatus.Conflict));

//        var result = await _processPaymentHandler.HandleAsync(command, CancellationToken.None);

//        Assert.Equal(PaymentOperationOutcome.Conflict, result.Outcome);
//        Assert.Equal("idempotency_conflict", result.Error!.Code);

//        _fraudServiceClient.Verify(
//            x => x.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()),
//            Times.Never);
//    }

//    [Fact]
//    public async Task HandleAsync_WhenDuplicateIdempotencyHasPayment_ShouldReturnExistingPayment()
//    {
//        var command = CreateValidCommand();

//        var existingPayment = new PaymentDto()
//        {
//            Id = Guid.NewGuid(),
//            Amount = command.Amount!.Value,
//            Currency = command.Currency
//        };

//        _idempotencyService
//            .Setup(x => x.TryAdd(command.IdempotencyKey!, It.IsAny<string>()))
//            .Returns(new IdempotencyResult(
//                IdempotencyStatus.Duplicate,
//                Payment: existingPayment));

//        var result = await _processPaymentHandler.HandleAsync(command, CancellationToken.None);

//        Assert.Equal(PaymentOperationOutcome.Ok, result.Outcome);
//        Assert.Same(existingPayment, result.Payment);

//        _fraudServiceClient.Verify(
//            x => x.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()),
//            Times.Never);

//        _paymentsRepository.Verify(
//            x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()),
//            Times.Never);
//    }

//    [Fact]
//    public async Task HandleAsync_WhenFraudServiceDeclines_ShouldReturnBadRequest()
//    {
//        var command = CreateValidCommand();

//        _fraudServiceClient
//            .Setup(x => x.CheckAsync(
//                It.IsAny<FraudCheckRequest>(),
//                It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new FraudCheckResponse(AuthorizationCode: null, Authorized: false));

//        var result = await _processPaymentHandler.HandleAsync(command, CancellationToken.None);

//        Assert.Equal(PaymentOperationOutcome.BadRequest, result.Outcome);
//        Assert.Equal("payment_declined", result.Error!.Code);

//        _acquiringBankClient.Verify(
//            x => x.ProcessAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()),
//            Times.Never);

//        _paymentsRepository.Verify(
//            x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()),
//            Times.Never);
//    }

//    [Fact]
//    public async Task HandleAsync_WhenFraudServiceReturnsNull_ShouldReturnBadRequest()
//    {
//        var command = CreateValidCommand();

//        _fraudServiceClient
//            .Setup(x => x.CheckAsync(
//                It.IsAny<FraudCheckRequest>(),
//                It.IsAny<CancellationToken>()))
//            .ReturnsAsync((FraudCheckResponse?)null);

//        var result = await _processPaymentHandler.HandleAsync(command, CancellationToken.None);

//        Assert.Equal(PaymentOperationOutcome.BadRequest, result.Outcome);
//        Assert.Equal("payment_declined", result.Error!.Code);
//    }

//    [Fact]
//    public async Task HandleAsync_WhenBankReturnsNull_ShouldReturnBadRequest()
//    {
//        var command = CreateValidCommand();

//        _acquiringBankClient
//            .Setup(x => x.ProcessAsync(
//                It.IsAny<BankPaymentRequest>(),
//                It.IsAny<CancellationToken>()))
//            .ReturnsAsync((BankPaymentResponse?)null);

//        var result = await _processPaymentHandler.HandleAsync(command, CancellationToken.None);

//        Assert.Equal(PaymentOperationOutcome.BadRequest, result.Outcome);
//        Assert.Equal("payment_rejected", result.Error!.Code);

//        _paymentsRepository.Verify(
//            x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()),
//            Times.Never);
//    }

//    [Fact]
//    public async Task HandleAsync_WhenBankDeclines_ShouldReturnBadRequest()
//    {
//        var command = CreateValidCommand();

//        _acquiringBankClient
//            .Setup(x => x.ProcessAsync(
//                It.IsAny<BankPaymentRequest>(),
//                It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new BankPaymentResponse(false, null));

//        var result = await _processPaymentHandler.HandleAsync(command, CancellationToken.None);

//        Assert.Equal(PaymentOperationOutcome.BadRequest, result.Outcome);
//        Assert.Equal("payment_declined", result.Error!.Code);

//        _paymentsRepository.Verify(
//            x => x.AddAsync(It.IsAny<Api.Domain.Entities.Payments.Payment>(), It.IsAny<CancellationToken>()),
//            Times.Never);
//    }

//    [Fact]
//    public async Task HandleAsync_WhenPersistenceFails_ShouldReturnServiceUnavailable()
//    {
//        var command = CreateValidCommand();

//        _unitOfWork
//            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
//            .ThrowsAsync(new Exception("database failure"));

//        var result = await _processPaymentHandler.HandleAsync(command, CancellationToken.None);

//        Assert.Equal(PaymentOperationOutcome.ServiceUnavailable, result.Outcome);
//        Assert.Equal("payment_failed", result.Error!.Code);

//        _idempotencyService.Verify(
//            x => x.TryUpdate(It.IsAny<PaymentDto>(), It.IsAny<string>(), It.IsAny<string>()),
//            Times.Never);
//    }

//    [Fact]
//    public async Task HandleAsync_WhenIdempotencyKeyIsNull_ShouldProcessWithoutIdempotencyService()
//    {
//        var command = CreateValidCommand(idempotencyKey: null);

//        var result = await _processPaymentHandler.HandleAsync(command, CancellationToken.None);

//        Assert.Equal(PaymentOperationOutcome.Created, result.Outcome);

//        _idempotencyService.Verify(
//            x => x.TryAdd(It.IsAny<string>(), It.IsAny<string>()),
//            Times.Never);

//        _idempotencyService.Verify(
//            x => x.TryUpdate(It.IsAny<PaymentDto>(), It.IsAny<string>(), It.IsAny<string>()),
//            Times.Never);
//    }

//    [Fact]
//    public async Task HandleAsync_ShouldSendUppercaseCurrencyToBank()
//    {
//        var command = CreateValidCommand(currency: "gbp");

//        await _processPaymentHandler.HandleAsync(command, CancellationToken.None);

//        _acquiringBankClient.Verify(
//            x => x.ProcessAsync(
//                It.Is<BankPaymentRequest>(request =>
//                    request.Currency == "GBP" &&
//                    request.ExpiryDate == "12/2030" &&
//                    request.Amount == command.Amount),
//                It.IsAny<CancellationToken>()),
//            Times.Once);
//    }

//    private static ProcessPaymentCommand CreateValidCommand(
//        string? idempotencyKey = "idem-key",
//        Guid? merchantId = null,
//        string cardNumber = "4242424242424242",
//        int? expiryMonth = 12,
//        int? expiryYear = 2030,
//        string cvv = "123",
//        long? amount = 100,
//        string currency = "GBP")
//    {
//        return new ProcessPaymentCommand
//        (
//            IdempotencyKey : idempotencyKey,
//            MerchantId : merchantId ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
//            CardNumber : cardNumber,
//            ExpiryMonth : expiryMonth,
//            ExpiryYear : expiryYear,
//            Cvv : cvv,
//            Amount : amount,
//            Currency : currency
//        );
//    }
//}