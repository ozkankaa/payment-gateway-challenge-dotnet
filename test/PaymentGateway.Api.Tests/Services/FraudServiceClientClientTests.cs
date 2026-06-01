using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;

using PaymentGateway.Api.Infrastructure.Services.FraudService;
using PaymentGateway.Api.Infrastructure.Services.FraudService.Requests;
using PaymentGateway.Api.Infrastructure.Services.FraudService.Responses;

namespace PaymentGateway.Api.Tests.Infrastructure.Services;

public class FraudServiceClientClientTests
{
    [Fact]
    public async Task FraudService_ReturnsServiceUnavailable()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri == new Uri("http://localhost:5000/frauds")),
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

        var logger = new Logger<FraudServiceClient>(new LoggerFactory());

        var fraudServiceClient = new FraudServiceClient(httpClient, logger);

        var request = new FraudCheckRequest(
            CardNumber: "4111111111111119"
        );

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => fraudServiceClient.CheckAsync(request, CancellationToken.None));

        // Assert
        Assert.NotNull(exception);
        Assert.Contains("Fraud service is unavailable", exception.Message);
    }

    [Fact]
    public async Task FraudService_ReturnsBadRequest()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri == new Uri("http://localhost:5000/frauds")),
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

        var logger = new Logger<FraudServiceClient>(new LoggerFactory());

        var fraudServiceClient = new FraudServiceClient(httpClient, logger);

        var request = new FraudCheckRequest(
            CardNumber: "0000000000000001"
        );

        // Act
        var response = await fraudServiceClient.CheckAsync(request, CancellationToken.None);

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task FraudService_ReturnsSuccess()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var authorizationCode = Guid.NewGuid().ToString()[..8].ToUpper();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri == new Uri("http://localhost:5000/frauds")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = JsonContent.Create(new FraudCheckResponse
                (
                    Authorized: true,
                    AuthorizationCode: authorizationCode
                ))
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var logger = new Logger<FraudServiceClient>(new LoggerFactory());

        var fraudServiceClient = new FraudServiceClient(httpClient, logger);

        var request = new FraudCheckRequest(
            CardNumber: "4111111111111113"
        );

        // Act
        var response = await fraudServiceClient.CheckAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.True(response!.Authorized);
        Assert.Equal(response.AuthorizationCode, authorizationCode);
    }
}

