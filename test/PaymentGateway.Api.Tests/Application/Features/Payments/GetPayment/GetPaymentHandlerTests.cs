using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Application.Abstractions.Persistence;
using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.GetPayment;
using PaymentGateway.Api.Domain.Entities.Payments;

namespace PaymentGateway.Api.Tests.Application.Features.Payments.GetPayment;

public class GetPaymentHandlerTests
{
    private readonly GetPaymentHandler _handler;
    private readonly Mock<IPaymentRepository> _paymentsRepositoryMock = new();
    private readonly Mock<ILogger<GetPaymentHandler>> _loggerMock = new();

    public GetPaymentHandlerTests()
    {
        _handler = new GetPaymentHandler(_paymentsRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenPaymentExists_ReturnsOkWithPayment()
    {
        // Arrange        
        var payment = Payment.Create("idempotency-key", Guid.NewGuid(), CardDetails.Create("1234", 11, 2030), Money.Create(100, "GBP"));
        var paymentId = payment.Id;
        var query = new GetPaymentQuery(paymentId);

        _paymentsRepositoryMock
            .Setup(x => x.GetByIdAsync(paymentId, TestContext.Current.CancellationToken))
            .ReturnsAsync(payment);

        // Act
        var result = await _handler.HandleAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentOperationOutcome.Ok, result.Outcome);
        Assert.Equal(payment.Id, result.Payment!.Id);

        _paymentsRepositoryMock.Verify(x => x.GetByIdAsync(paymentId, TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPaymentDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var query = new GetPaymentQuery(paymentId);

        _paymentsRepositoryMock
            .Setup(x => x.GetByIdAsync(paymentId,TestContext.Current.CancellationToken))
            .ReturnsAsync((Payment?)null);

        // Act
        var result = await _handler.HandleAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentOperationOutcome.NotFound, result.Outcome);
        Assert.Null(result.Payment);

        _paymentsRepositoryMock.Verify(x => x.GetByIdAsync(paymentId, TestContext.Current.CancellationToken), Times.Once);
    }

}
