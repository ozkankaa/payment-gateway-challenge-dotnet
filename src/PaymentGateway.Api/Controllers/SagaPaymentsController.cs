using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Asp.Versioning;

using MassTransit;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.GetPayment;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Extensions;
using PaymentGateway.Api.Infrastructure.Metrics;
using PaymentGateway.Api.Infrastructure.Services.ETagService;
using PaymentGateway.Api.Mappers;
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
    IETagService etagService,
    ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpGet("{id:guid}", Name = nameof(GetPayment))]
    [ProducesResponseType(typeof(PostPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostPaymentResponse>> GetPayment(
        [FromRoute] Guid id,
        [FromServices] GetPaymentHandler handler, CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrieving payment {PaymentId}.", id);

        var result = await handler.HandleAsync(new GetPaymentQuery(id), cancellationToken);

        if (result.Outcome == PaymentOperationOutcome.NotFound)
            return HandleGetPaymentResult(id, result);

        var etag = etagService.Generate(result);

        if (etagService.Matches(Request, etag))
        {
            PaymentMetrics.AddPaymentTotal("retrieve", "payment_not_modified");
            logger.LogInformation("Payment {PaymentId} was not modified.", id);
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;

        return HandleGetPaymentResult(id, result);
    }

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
            return BadRequest(new ErrorResponse("payment_bad_request", "Idempotency key is required in the 'X-Request-ID' header."));
        }


        var lastFourDigits = request.CardNumber.LastFourDigits();
        var cardToken = $"{lastFourDigits}_token";
        var paymentId = Guid.NewGuid(); 
        var correlationId = Guid.NewGuid(); 
        var requestHash = CreateRequestHash(request);

        logger.LogInformation(
            "Processing payment for merchant {MerchantId} with idempotency key {IdempotencyKey} and card ending {LastFourDigits}.",
            request.MerchantId,
            idempotencyKey,
            lastFourDigits);

        await publishEndpoint.Publish(new StartPayment(
            correlationId,
            paymentId,
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

        return AcceptedAtAction(
            nameof(GetPayment),
            new { id = paymentId },
            new
            {
                id = paymentId,
                status = "Pending"
            });
    }

    private ActionResult<PostPaymentResponse> HandleGetPaymentResult(
        Guid paymentId,
        PaymentOperationResultDto result)
    {
        return result.Outcome switch
        {
            PaymentOperationOutcome.Ok => WithMetric("retrieve", "payment_retrieved", () =>
            {
                logger.LogInformation("Payment {PaymentId} retrieved successfully.", paymentId);
                return Ok(result.Payment!.ToResponse());
            }),

            PaymentOperationOutcome.NotModified => WithMetric("retrieve", "payment_not_modified", () =>
            {
                logger.LogInformation("Payment {PaymentId} was not modified.", paymentId);
                return StatusCode(StatusCodes.Status304NotModified);
            }),

            PaymentOperationOutcome.NotFound => WithMetric("retrieve", "payment_not_found", () =>
            {
                logger.LogInformation("Payment {PaymentId} was not found.", paymentId);
                return NotFound();
            }),

            _ => WithMetric("retrieve", "payment_unknown", () =>
            {
                logger.LogError(
                    "Unexpected outcome {Outcome} while retrieving payment {PaymentId}.",
                    result.Outcome,
                    paymentId);

                return StatusCode(StatusCodes.Status500InternalServerError);
            })
        };
    }

    private static ActionResult<PostPaymentResponse> WithMetric(
        string operation,
        string outcome,
        Func<ActionResult<PostPaymentResponse>> action)
    {
        PaymentMetrics.AddPaymentTotal(operation, outcome);
        return action();
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
