using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;

using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService;
using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Requests;
using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Responses;

namespace PaymentGateway.Api.Tests.Infrastructure.Services;

public class AcquiringBankClientTests
{
    [Fact]
    public async Task BankPayment_ReturnsServiceUnavailable()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri == new Uri("http://localhost:5000/payments")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.ServiceUnavailable,
                Content = new StringContent("Service Unavailable")
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var logger = new Logger<AcquiringBankClient>(new LoggerFactory());

        var acquiringBankClient = new AcquiringBankClient(httpClient, logger);

        var request = new BankPaymentRequest(
            Amount: 100,
            Currency: "USD",
            CardNumber: "4111111111111111",
            ExpiryDate: "12/25",
            Cvv: "123"
        );

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => acquiringBankClient.ProcessAsync(request, CancellationToken.None));

        // Assert
        Assert.NotNull(exception);
        Assert.Contains("Acquiring bank is unavailable", exception.Message);
    }

    [Fact]
    public async Task BankPayment_ReturnsBadRequest()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri == new Uri("http://localhost:5000/payments")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.BadRequest,
                Content = new StringContent("Bad Request")
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var logger = new Logger<AcquiringBankClient>(new LoggerFactory());

        var acquiringBankClient = new AcquiringBankClient(httpClient, logger);

        var request = new BankPaymentRequest(
            Amount: 0,
            Currency: "ABC",
            CardNumber: "0000000000000000",
            ExpiryDate: "00/00",
            Cvv: "000"
        );

        // Act
        var response = await acquiringBankClient.ProcessAsync(request, CancellationToken.None);

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task BankPayment_ReturnsSuccess()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var authorizationCode = Guid.NewGuid().ToString()[..8].ToUpper();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri == new Uri("http://localhost:5000/payments")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = JsonContent.Create(new BankPaymentResponse
                (
                    Authorized: true,
                    AuthorizationCode: authorizationCode
                ))
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var logger = new Logger<AcquiringBankClient>(new LoggerFactory());

        var acquiringBankClient = new AcquiringBankClient(httpClient, logger);

        var request = new BankPaymentRequest(
            Amount: 100,
            Currency: "USD",
            CardNumber: "4111111111111111",
            ExpiryDate: "12/25",
            Cvv: "123"
        );

        // Act
        var response = await acquiringBankClient.ProcessAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.True(response!.Authorized);
        Assert.Equal(response.AuthorizationCode, authorizationCode);
    }
}

