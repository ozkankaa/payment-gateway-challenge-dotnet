using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Api.Services;

public sealed class ETagService : IETagService
{
    public string Generate<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return $"\"{Convert.ToHexString(hash)}\"";
    }

    public bool Matches(HttpRequest request, string etag)
    {
        return request.Headers.IfNoneMatch == etag;
    }
}
