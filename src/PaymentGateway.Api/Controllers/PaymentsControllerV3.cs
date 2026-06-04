using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Asp.Versioning;

using MassTransit;

using Microsoft.AspNetCore.Mvc;
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
using PaymentGateway.Api.Saga.Request.Messages;

namespace PaymentGateway.Api.Controllers;

[ApiController]
[ApiVersion("3.0")]
[Route("api/v{version:apiVersion}/payments")]
[Produces("application/json")]
[EnableRateLimiting("PaymentsRateLimit")]
public class PaymentsControllerV3(
    IRequestClient<StartPaymentRequest> paymentClient,
    IETagService etagService,
    ILogger<PaymentsControllerV1> logger) : ControllerBase
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

        try
        {
            var response = await paymentClient.GetResponse<PaymentSucceededResponse, PaymentFailedResponse>(
                    new StartPaymentRequest(
                        correlationId,
                        paymentId,
                        request.MerchantId,
                        cardToken,
                        lastFourDigits,
                        request.CardNumber,
                        (int)request.ExpiryMonth!,
                        (int)request.ExpiryYear!,
                        request.Currency,
                        (long)request.Amount!,
                        request.Cvv,
                        idempotencyKey,
                        requestHash
                    ),
                    cancellationToken);

            return response.Is(out Response<PaymentSucceededResponse> success)
                ? (ActionResult<PostPaymentResponse>)Ok(new PostPaymentResponse
                {
                    Id = success.Message.Payement.Id,
                    CardNumberLastFour = success.Message.Payement.CardNumberLastFour.ToString("D4"),
                    Currency = success.Message.Payement.Currency,
                    Amount = success.Message.Payement.Amount,
                    ExpiryMonth = success.Message.Payement.ExpiryMonth.ToString("D2"),
                    ExpiryYear = success.Message.Payement.ExpiryYear.ToString("D4"),
                    Status = success.Message.Payement.Status
                })
                : response.Is(out Response<PaymentFailedResponse> failed)
                ? (ActionResult<PostPaymentResponse>)BadRequest(new ErrorResponse(
                    Code: failed.Message.Error?.Code ?? "payment_processing_failed",
                    Message: failed.Message.Error?.Message ?? "Payment processing failed.",
                    Errors: failed.Message.Error?.Errors
                ))
                : (ActionResult<PostPaymentResponse>)StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("payment_processing_failed", "Payment processing failed with an unknown error."));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Payment processing failed with an unexpected error, idempotency key {IdempotencyKey}, card ending {LastFourDigits}.",
                idempotencyKey,
                lastFourDigits);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("payment_service_unavailable", "Payment service is currently unavailable. Please try again later."));
        }
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
