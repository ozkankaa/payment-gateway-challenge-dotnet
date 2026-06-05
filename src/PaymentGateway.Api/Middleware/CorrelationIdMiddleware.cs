using System.Diagnostics;

using Serilog.Context;

namespace PaymentGateway.Api.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CORRELATION_ID = "X-Correlation-ID";

    public async Task Invoke(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(CORRELATION_ID, out var existing)
                ? existing.ToString()
                : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Request.Headers[CORRELATION_ID] = correlationId;
        context.Response.Headers[CORRELATION_ID] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}