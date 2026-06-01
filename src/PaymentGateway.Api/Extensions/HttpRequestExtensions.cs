using Microsoft.Extensions.Primitives;

namespace PaymentGateway.Api.Extensions;

public static class HttpRequestExtensions
{
    public static string? GetIdempotencyKey(this HttpRequest request)
    {
        return request.Headers.TryGetValue("Idempotency-Key", out StringValues value)
            ? value.ToString()
            : null;
    }
}