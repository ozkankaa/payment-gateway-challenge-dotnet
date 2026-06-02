using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Asp.Versioning;

using MassTransit;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Extensions;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Saga;

namespace PaymentGateway.Api.Controllers;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/payments")]
[Produces("application/json")]
[EnableRateLimiting("PaymentsRateLimit")]
public class SagaPaymentsController(
    IPublishEndpoint publishEndpoint,
    ISendEndpointProvider sendEndpointProvider,
    ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PostPaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(PostPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        [FromBody] PostPaymentRequest request,
        [FromServices] ProcessPaymentHandler handler,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = Request.GetIdempotencyKey();

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new ErrorResponse("payment_bad_request","Idempotency key is required in the 'X-Request-ID' header."));
        }

        
        var lastFourDigits = request.CardNumber.LastFourDigits();
        var cardToken = $"{lastFourDigits}_token";

        var correlationId = Guid.NewGuid(); //$"{idempotencyKey}_{request.MerchantId}_{lastFourDigits}_{request.Amount}_{request.Currency}";
        var requestHash = CreateRequestHash(request);

        logger.LogInformation(
            "Processing payment for merchant {MerchantId} with idempotency key {IdempotencyKey} and card ending {LastFourDigits}.",
            request.MerchantId,
            idempotencyKey,
            lastFourDigits);

    //    var endpoint = await sendEndpointProvider.GetSendEndpoint(
    //new Uri("queue:debug-start-payment"));

    //    await endpoint.Send(new StartPayment(
    //        correlationId,
    //        request.MerchantId,
    //        request.CardNumber,
    //        1,
    //        2027,
    //        request.Currency,
    //        100,
    //        request.Cvv,
    //        idempotencyKey,
    //        requestHash));

    //    //var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:payment-saga"));

    //    //await endpoint.Send(new StartPayment(
    //    //    correlationId,
    //    //    request.MerchantId,
    //    //    request.CardNumber,
    //    //    (int)request.ExpiryMonth,
    //    //    (int)request.ExpiryYear,
    //    //    request.Currency,
    //    //    (long)request.Amount,
    //    //    request.Cvv,
    //    //    idempotencyKey,
    //    //    requestHash));


        await publishEndpoint.Publish(new StartPayment(
            correlationId,
            request.MerchantId,
            cardToken,
            lastFourDigits,
            request.CardNumber,
            (int)request.ExpiryMonth,
            (int)request.ExpiryYear,
            request.Currency,
            (long)request.Amount,
            request.Cvv,
            idempotencyKey,
            requestHash));

        return Accepted(new
        {
            status = "Pending"
        });
    }

    private static string CreateRequestHash(PostPaymentRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            request.MerchantId,
            request.CardNumber,
            request.ExpiryMonth,
            request.ExpiryYear,
            request.Cvv,
            request.Amount,
            Currency = request.Currency.ToUpperInvariant()
        });

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(bytes);
    }
}
