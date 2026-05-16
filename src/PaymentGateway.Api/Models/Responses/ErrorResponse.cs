namespace PaymentGateway.Api.Models.Responses;

public sealed record ErrorResponse(
    string Code,
    string Message,
    IDictionary<string, string[]>? Errors = null);
