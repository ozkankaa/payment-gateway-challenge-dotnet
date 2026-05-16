namespace PaymentGateway.Api.Services;

public interface IETagService
{
    string Generate<T>(T value);
    bool Matches(HttpRequest request, string etag);
}
