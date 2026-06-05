namespace PaymentGateway.Api.Middleware;

public class CorrelationIdHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    private const string CORRELATION_ID = "X-Correlation-ID";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId =
            httpContextAccessor.HttpContext?
                .Request.Headers[CORRELATION_ID]
                .ToString();

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation(
                CORRELATION_ID,
                correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}