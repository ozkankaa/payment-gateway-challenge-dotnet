using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

using PaymentGateway.Api.Application.Payments.Dtos;
using PaymentGateway.Api.Application.Payments.GetPayment;
using PaymentGateway.Api.Application.Payments.ProcessPayment;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
[Produces("application/json")]
[EnableRateLimiting("PaymentsRateLimit")]
public class PaymentsController(
    IOutputCacheStore cache,
    IETagService etagService,
    ILogger<PaymentsController> logger) : Controller
{
    [HttpGet("{id:guid}", Name = "GetPayment")]
    [ProducesResponseType(typeof(PostPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [OutputCache(Duration = 60, PolicyName = "payments")]
    public ActionResult<PostPaymentResponse> GetPayment(
        [FromRoute] Guid id,
        [FromServices] GetPaymentHandler handler)
    {
        logger.LogInformation("Retrieving payment with id {Id}", id);

        var result = handler.Handle(new GetPaymentQuery(id));

        var etag = etagService.Generate(result);

        if (etagService.Matches(Request, etag))
        {
            logger.LogInformation("Retrieving payment {PaymentId} not modified.", id);
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;

        switch (result.Outcome)
        {
            case PaymentOperationOutcome.Ok:
                logger.LogInformation("Retrieved payment with id {PaymentId} and outcome {Outcome}.", id, result.Outcome);
                return Ok(result.Payment);
            case PaymentOperationOutcome.NotModified:
                logger.LogInformation("Retrieving payment with id {PaymentId} not modified and outcome {Outcome}.", id, result.Outcome);
                return StatusCode(StatusCodes.Status304NotModified);
            case PaymentOperationOutcome.NotFound:
                logger.LogInformation("Retrieving payment with id {PaymentId} not found and outcome {Outcome}.", id, result.Outcome);
                return NotFound();
            default:
                logger.LogError("Retrieving payment with id {PaymentId} not proced with outcome {Outcome} and returns server error.", id, result.Outcome);
                return StatusCode(StatusCodes.Status500InternalServerError);
        }
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
        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var value) ? value.ToString() : null;
        var lastFourCardDigits = request.CardNumber.Length >= 4 ? request.CardNumber[^4..] : request.CardNumber;

        logger.LogInformation("Creating payment with Idempotency-Key {IdempotencyKey} and last four digit card number {LastFourCardDigits}.", idempotencyKey, lastFourCardDigits);

        var command = new ProcessPaymentCommand(
            CardNumber: request.CardNumber,
            ExpiryMonth: request.ExpiryMonth,
            ExpiryYear: request.ExpiryYear,
            Currency: request.Currency,
            Amount: request.Amount,
            Cvv: request.Cvv,
            IdempotencyKey: idempotencyKey);

        var result = handler.Handle(command);

        if (result.Outcome == PaymentOperationOutcome.Created || result.Outcome == PaymentOperationOutcome.Ok)
        {
            logger.LogInformation("Creating payment with Idempotency-Key {IdempotencyKey} and last four digit card number {LastFourCardDigits} processed successfully with payment id {PaymentId} and outcome {Outcome} then refreshing the cache.", idempotencyKey, lastFourCardDigits, result.Payment!.Id, result.Outcome);
            await cache.EvictByTagAsync("payments", cancellationToken);
        }
        var errors = string.Empty;
        errors = $"{result.Error?.Code}: {result.Error?.Message}{Environment.NewLine}";
        errors +=
            (result.Error?.Errors == null
                ? ""
                : string.Join(Environment.NewLine, result.Error.Errors.Select(x => $"{x.Key}: {string.Join(", ", x.Value)}")));

        switch (result.Outcome)
        {
            case PaymentOperationOutcome.Created:
                logger.LogInformation("Creating payment with Idempotency-Key {IdempotencyKey} and last four digit card number {LastFourCardDigits} processed successfully with payment id {PaymentId} and outcome {Outcome}.", idempotencyKey, lastFourCardDigits, result.Payment!.Id, result.Outcome);
                return CreatedAtAction("GetPayment", new { id = result.Payment!.Id }, result.Payment);
            case PaymentOperationOutcome.Ok:
                logger.LogInformation("Creating payment with Idempotency-Key {IdempotencyKey} and last four digit card number {LastFourCardDigits} processed successfully with payment id {PaymentId} and outcome {Outcome}.", idempotencyKey, lastFourCardDigits, result.Payment!.Id, result.Outcome);
                return Ok(result.Payment);
            case PaymentOperationOutcome.BadRequest:
                logger.LogInformation("Creating payment with Idempotency-Key {IdempotencyKey} and last four digit card number {LastFourCardDigits} not processed successfully with errors {Errors} and outcome {Outcome}.", idempotencyKey, lastFourCardDigits, errors, result.Outcome);
                return BadRequest(result.Error);
            case PaymentOperationOutcome.Conflict:
                logger.LogInformation("Creating payment with Idempotency-Key {IdempotencyKey} and last four digit card number {LastFourCardDigits} has conflict with error {ErrorMessage} and outcome {Outcome}.", idempotencyKey, lastFourCardDigits, errors, result.Outcome);
                return Conflict(result.Error);
            case PaymentOperationOutcome.ServiceUnavailable:
                logger.LogInformation("Creating payment with Idempotency-Key {IdempotencyKey} and last four digit card number {LastFourCardDigits} has service unavailable problem with errors {ErrorMessage} and outcome {Outcome}.", idempotencyKey, lastFourCardDigits, errors, result.Outcome);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, result.Error);
            default:
                logger.LogError("Creating payment with Idempotency-Key {IdempotencyKey} and last four digit card number {LastFourCardDigits} raising internal error.", idempotencyKey, lastFourCardDigits);
                return StatusCode(StatusCodes.Status500InternalServerError);
        }
        ;
    }
}