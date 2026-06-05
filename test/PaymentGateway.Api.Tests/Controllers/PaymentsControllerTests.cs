using System.Net;
using System.Net.Http.Json;

using PaymentGateway.Api.Infrastructure;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Tests.Common;

namespace PaymentGateway.Api.Tests.Controllers;

[Collection("Integration tests")]
public class PaymentsControllerTests : IClassFixture<PaymentGatewayFactory>
{
    private readonly HttpClient _client;
    private const string PaymentsUrl = "/api/v1/payments";

    public PaymentsControllerTests(PaymentGatewayFactory factory)
    {
        _client = factory.CreateClient();
        FakeAcquiringBankClient.Reset();
    }

    #region PostPaymentAsync

    [Fact]
    public async Task PostPayment_AuthorizesOddLastCardDigit_AndCanBeRetrieved()
    {
        // Arrange
        var postPaymentRequest = new PostPaymentRequest(
            MerchantId: Guid.NewGuid(),
             CardNumber: "2222405343248877",
             ExpiryMonth: 12,
             ExpiryYear: 2030,
             Currency: "USD",
             Amount: 100,
             Cvv: "123");

        var postMessage = new HttpRequestMessage(HttpMethod.Post, $"{PaymentsUrl}");
        postMessage.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        postMessage.Content = JsonContent.Create(postPaymentRequest);

        // Act
        var response = await _client.SendAsync(postMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonDefaults.Options, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatusEnum.Authorized, payment!.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal("USD", payment.Currency);

        var getResponse = await _client.GetAsync($"{PaymentsUrl}/{payment.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task PostPayment_DeclinesEvenLastCardDigit()
    {
        // Arrange
        var postPaymentRequest = new PostPaymentRequest(
             MerchantId: Guid.NewGuid(),
             CardNumber: "2222405343248878",
             ExpiryMonth: 12,
             ExpiryYear: 2030,
             Currency: "USD",
             Amount: 100,
             Cvv: "123");

        var postMessage = new HttpRequestMessage(HttpMethod.Post, $"{PaymentsUrl}");
        postMessage.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        postMessage.Content = JsonContent.Create(postPaymentRequest);

        // Act
        var response = await _client.SendAsync(postMessage, TestContext.Current.CancellationToken);

        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonDefaults.Options, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("payment_declined", error!.Code);
    }

    [Fact]
    public async Task PostPayment_Returns400_WhenRequestInvalid_AndDoesNotCallBank()
    {
        // Arrange
        var postPaymentRequest = new PostPaymentRequest(
             MerchantId: Guid.NewGuid(),
             CardNumber: "123",
             ExpiryMonth: 12,
             ExpiryYear: 2030,
             Currency: "USD",
             Amount: 100,
             Cvv: "123");

        var postMessage = new HttpRequestMessage(HttpMethod.Post, $"{PaymentsUrl}");
        postMessage.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        postMessage.Content = JsonContent.Create(postPaymentRequest);

        // Act
        var response = await _client.SendAsync(postMessage, TestContext.Current.CancellationToken);        

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonDefaults.Options, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("payment_rejected", error!.Code);
        Assert.Contains(nameof(PostPaymentRequest.CardNumber), error.Errors!.Keys);
    }

    [Fact]
    public async Task PostPayment_Returns503_WhenBankUnavailable()
    {
        // Arrange
        var postPaymentRequest = new PostPaymentRequest(
             MerchantId: Guid.NewGuid(),
             CardNumber: "2222405343248870",
             ExpiryMonth: 12,
             ExpiryYear: 2030,
             Currency: "USD",
             Amount: 100,
             Cvv: "123");

        var postMessage = new HttpRequestMessage(HttpMethod.Post, $"{PaymentsUrl}");
        postMessage.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        postMessage.Content = JsonContent.Create(postPaymentRequest);

        // Act
        var response = await _client.SendAsync(postMessage, TestContext.Current.CancellationToken);        

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_IsIdempotent_WhenSameKeyAndSameBodyAreReplayed()
    {
        // Arrange
        var key = $"payment-{Guid.NewGuid():N}";
        var firstPaymentRequest = new PostPaymentRequest(
             MerchantId: Guid.NewGuid(),
             CardNumber: "2222405343248877",
             ExpiryMonth: 12,
             ExpiryYear: 2030,
             Currency: "USD",
             Amount: 100,
             Cvv: "123");

        // Act
        var first = new HttpRequestMessage(HttpMethod.Post, PaymentsUrl) { Content = JsonContent.Create(firstPaymentRequest) };
        first.Headers.Add("Idempotency-Key", key);
        var firstResponse = await _client.SendAsync(first, TestContext.Current.CancellationToken);
        var firstPayment = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonDefaults.Options, cancellationToken: TestContext.Current.CancellationToken);

        var secondPaymentRequest = new PostPaymentRequest(
             MerchantId: firstPaymentRequest.MerchantId,
             CardNumber: "2222405343248877",
             ExpiryMonth: 12,
             ExpiryYear: 2030,
             Currency: "USD",
             Amount: 100,
             Cvv: "123");

        var second = new HttpRequestMessage(HttpMethod.Post, PaymentsUrl) { Content = JsonContent.Create(secondPaymentRequest) };
        second.Headers.Add("Idempotency-Key", key);
        var secondResponse = await _client.SendAsync(second, TestContext.Current.CancellationToken);
        var secondPayment = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonDefaults.Options, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstPayment!.Id, secondPayment!.Id);
    }

    [Fact]
    public async Task PostPayment_Returns409_WhenIdempotencyKeyIsReusedWithDifferentBody()
    {
        // Arrange
        var key = $"payment-{Guid.NewGuid():N}";
        var firstPaymentRequest = new PostPaymentRequest(
             MerchantId: Guid.NewGuid(),
             CardNumber: "2222405343248875",
             ExpiryMonth: 12,
             ExpiryYear: 2030,
             Currency: "USD",
             Amount: 100,
             Cvv: "123");
        var first = new HttpRequestMessage(HttpMethod.Post, PaymentsUrl) { Content = JsonContent.Create(firstPaymentRequest) };
        first.Headers.Add("Idempotency-Key", key);
        await _client.SendAsync(first, TestContext.Current.CancellationToken);

        var secondPaymentRequest = new PostPaymentRequest(
             MerchantId: Guid.NewGuid(),
             CardNumber: "2222405343248877",
             ExpiryMonth: 12,
             ExpiryYear: 2030,
             Currency: "USD",
             Amount: 100,
             Cvv: "123");
        var second = new HttpRequestMessage(HttpMethod.Post, PaymentsUrl) { Content = JsonContent.Create(secondPaymentRequest) };
        second.Headers.Add("Idempotency-Key", key);
        var response = await _client.SendAsync(second, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    #endregion

    #region GetPaymentAsync

    [Fact]
    public async Task GetPayment_Returns404_WhenPaymentDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync($"{PaymentsUrl}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPayment_Returns200_WhenPaymentDoesExist()
    {
        // Arrange
        var postPaymentRequest = new PostPaymentRequest(
            MerchantId: Guid.NewGuid(),
            CardNumber: "2222405343248877",
            ExpiryMonth: 12,
            ExpiryYear: 2030,
            Currency: "USD",
            Amount: 100,
            Cvv: "123");

        var postMessage = new HttpRequestMessage(HttpMethod.Post, $"{PaymentsUrl}");
        postMessage.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        postMessage.Content = JsonContent.Create(postPaymentRequest);
        var postResponse = await _client.SendAsync(postMessage, TestContext.Current.CancellationToken);
        var paymentPost = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonDefaults.Options, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var getMessage = new HttpRequestMessage(HttpMethod.Get, $"{PaymentsUrl}/{paymentPost!.Id}");
        var getResponse = await _client.SendAsync(getMessage, TestContext.Current.CancellationToken);
        var paymentGet = await getResponse.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonDefaults.Options, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(paymentPost.Id, paymentGet!.Id);
    }

    #endregion

    #region Health and OpenAPI

    [Fact]
    public async Task HealthAndOpenApi_AreAvailable()
    {
        Assert.Equal(
        HttpStatusCode.OK,
        (await _client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);        
    }

    #endregion

    #region Other

    [Fact]
    public async Task PostPayment_IsConcurrencySafe_ForSameIdempotencyKey()
    {
        var key = $"payment-{Guid.NewGuid():N}";
        var postPaymentRequest = new PostPaymentRequest(
             MerchantId: Guid.NewGuid(),
             CardNumber: "2222405343248877",
             ExpiryMonth: 12,
             ExpiryYear: 2030,
             Currency: "USD",
             Amount: 100,
             Cvv: "123");
        var tasks = Enumerable.Range(0, 2).Select(_ =>
        {
            var message = new HttpRequestMessage(HttpMethod.Post, PaymentsUrl) { Content = JsonContent.Create(postPaymentRequest) };
            message.Headers.Add("Idempotency-Key", key);
            return _client.SendAsync(message);
        }).ToArray();

        var responses = await Task.WhenAll(tasks);
        var payments = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonDefaults.Options, cancellationToken: TestContext.Current.CancellationToken)));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));
        Assert.Single(payments.Select(p => p!.Id).Distinct());
        Assert.Equal(1, FakeAcquiringBankClient.CallCounts);
    }

    [Fact]
    public async Task PostPayment_FractionalAmount_ReturnsBadRequest()
    {
        // Arrange
        var postPaymentRequest = new
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2030,
            Currency = "USD",
            Amount = 10.50, // fractional value -> invalid for long/major-minor contract
            Cvv = "123"
        };

        var message = new HttpRequestMessage(HttpMethod.Post, PaymentsUrl) { Content = JsonContent.Create(postPaymentRequest) };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        // Act
        var response = await _client.SendAsync(message, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion
}
