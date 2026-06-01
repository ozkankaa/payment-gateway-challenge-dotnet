using Asp.Versioning;

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

namespace PaymentGateway.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
[Produces("application/json")]
[EnableRateLimiting("PaymentsRateLimit")]
public sealed class PaymentsController(
    IOutputCacheStore cache,
    IETagService etagService,
    ILogger<PaymentsController> logger) : ControllerBase
{
    private const string PaymentsCacheTag = "payments";

    [HttpGet("{id:guid}", Name = nameof(GetPayment))]
    [ProducesResponseType(typeof(PostPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(Duration = 60, PolicyName = PaymentsCacheTag)]
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
        var lastFourDigits = request.CardNumber.LastFourDigits();

        logger.LogInformation(
            "Processing payment for merchant {MerchantId} with idempotency key {IdempotencyKey} and card ending {LastFourDigits}.",
            request.MerchantId,
            idempotencyKey,
            lastFourDigits);

        var command = request.ToCommand(idempotencyKey);
        var result = await handler.HandleAsync(command, cancellationToken);

        if (IsSuccessfulPayment(result.Outcome))
            await cache.EvictByTagAsync(PaymentsCacheTag, cancellationToken);

        return HandleProcessPaymentResult(result, idempotencyKey, lastFourDigits);
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

    private ActionResult<PostPaymentResponse> HandleProcessPaymentResult(
        PaymentOperationResultDto result,
        string? idempotencyKey,
        string lastFourDigits)
    {
        return result.Outcome switch
        {
            PaymentOperationOutcome.Created => WithMetric("process", "payment_created", () =>
            {
                var response = result.Payment!.ToResponse();

                logger.LogInformation(
                    "Payment {PaymentId} created successfully with idempotency key {IdempotencyKey} and card ending {LastFourDigits}.",
                    response.Id,
                    idempotencyKey,
                    lastFourDigits);

                return CreatedAtAction(
                    nameof(GetPayment),
                    new
                    {
                        version = HttpContext.GetRequestedApiVersion()?.ToString(),
                        id = response.Id
                    },
                    response);
            }),

            PaymentOperationOutcome.Ok => WithMetric("process", "payment_ok", () =>
            {
                var response = result.Payment!.ToResponse();

                logger.LogInformation(
                    "Payment {PaymentId} returned from idempotent request with idempotency key {IdempotencyKey}.",
                    response.Id,
                    idempotencyKey);

                return Ok(response);
            }),

            PaymentOperationOutcome.BadRequest => WithMetric(
                "process",
                result.Error?.Code ?? "payment_bad_request",
                () =>
                {
                    LogPaymentFailure(result, idempotencyKey, lastFourDigits);
                    return BadRequest(result.Error);
                }),

            PaymentOperationOutcome.Conflict => WithMetric(
                "process",
                result.Error?.Code ?? "payment_conflict",
                () =>
                {
                    LogPaymentFailure(result, idempotencyKey, lastFourDigits);
                    return Conflict(result.Error);
                }),

            PaymentOperationOutcome.ServiceUnavailable => WithMetric(
                "process",
                result.Error?.Code ?? "bank_unavailable",
                () =>
                {
                    LogPaymentFailure(result, idempotencyKey, lastFourDigits);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, result.Error);
                }),

            _ => WithMetric("process", result.Error?.Code ?? "payment_unknown", () =>
            {
                logger.LogError(
                    "Unexpected outcome {Outcome} while processing payment with idempotency key {IdempotencyKey}. Error: {ErrorCode} - {ErrorMessage}",
                    result.Outcome,
                    idempotencyKey,
                    result.Error?.Code,
                    result.Error?.Message);

                return StatusCode(StatusCodes.Status500InternalServerError);
            })
        };
    }

    private static bool IsSuccessfulPayment(PaymentOperationOutcome outcome)
    {
        return outcome is PaymentOperationOutcome.Created or PaymentOperationOutcome.Ok;
    }

    private void LogPaymentFailure(
        PaymentOperationResultDto result,
        string? idempotencyKey,
        string lastFourDigits)
    {
        logger.LogInformation(
            "Payment processing failed with outcome {Outcome}, error code {ErrorCode}, error message {ErrorMessage}, idempotency key {IdempotencyKey}, card ending {LastFourDigits}.",
            result.Outcome,
            result.Error?.Code,
            result.Error?.Message,
            idempotencyKey,
            lastFourDigits);
    }

    private static ActionResult<PostPaymentResponse> WithMetric(
        string operation,
        string outcome,
        Func<ActionResult<PostPaymentResponse>> action)
    {
        PaymentMetrics.AddPaymentTotal(operation, outcome);
        return action();
    }
}