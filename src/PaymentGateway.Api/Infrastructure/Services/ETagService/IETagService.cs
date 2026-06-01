namespace PaymentGateway.Api.Infrastructure.Services.ETagService;

public interface IETagService
{
    string Generate<T>(T value);
    bool Matches(HttpRequest request, string etag);
}
