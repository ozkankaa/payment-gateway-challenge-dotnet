using System.Diagnostics;

using Microsoft.Net.Http.Headers;

using Serilog.Context;

public class CorrelationIdMiddleware
{
    private const string CORRELATION_ID = "X-Correlation-ID";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

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
            await _next(context);
        }
    }
}