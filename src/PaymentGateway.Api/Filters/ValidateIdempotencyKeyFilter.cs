using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Filters;

public sealed class ValidateIdempotencyKeyFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method)) return;
        if (!context.HttpContext.Request.Path.Value?.Contains("/payments", StringComparison.OrdinalIgnoreCase) ?? true) return;

        if (!context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var value)) return;

        var key = value.ToString();
        if (key.Length is < 8 or > 128)
        {
            context.Result = new BadRequestObjectResult(new ErrorResponse(
                "invalid_idempotency_key",
                "Idempotency-Key must be between 8 and 128 characters."));
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
