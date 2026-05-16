using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Application.Payments.Dtos;
using PaymentGateway.Api.Application.Payments.GetPayment;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymenGateway.Api.Tests.Application.Payments.GetPayment;

public class GetPaymentHandlerTests
{
    private readonly GetPaymentHandler _handler;
    private readonly Mock<IPaymentsRepository> _paymentsRepositoryMock = new();
    private readonly Mock<ILogger<GetPaymentHandler>> _loggerMock = new();

    public GetPaymentHandlerTests()
    {
        _handler = new GetPaymentHandler(_paymentsRepositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Handle_WhenPaymentExists_ReturnsOkWithPayment()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var query = new GetPaymentQuery(paymentId);

        var payment = new PostPaymentResponse
        {
            Id = paymentId
        };

        _paymentsRepositoryMock
            .Setup(x => x.TryGet(paymentId))
            .Returns(payment);

        // Act
        var result = _handler.Handle(query);

        // Assert
        Assert.Equal(PaymentOperationOutcome.Ok, result.Outcome);
        Assert.Equal(payment, result.Payment);

        _paymentsRepositoryMock.Verify(x => x.TryGet(paymentId), Times.Once);
    }

    [Fact]
    public void Handle_WhenPaymentDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var query = new GetPaymentQuery(paymentId);

        _paymentsRepositoryMock
            .Setup(x => x.TryGet(paymentId))
            .Returns((PostPaymentResponse?)null);

        // Act
        var result = _handler.Handle(query);

        // Assert
        Assert.Equal(PaymentOperationOutcome.NotFound, result.Outcome);
        Assert.Null(result.Payment);

        _paymentsRepositoryMock.Verify(x => x.TryGet(paymentId), Times.Once);
    }

}
