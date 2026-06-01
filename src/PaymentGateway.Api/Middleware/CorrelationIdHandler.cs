namespace PaymentGateway.Api.Middleware;

public class CorrelationIdHandler : DelegatingHandler
{
    private const string CORRELATION_ID = "X-Correlation-ID";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId =
            _httpContextAccessor.HttpContext?
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