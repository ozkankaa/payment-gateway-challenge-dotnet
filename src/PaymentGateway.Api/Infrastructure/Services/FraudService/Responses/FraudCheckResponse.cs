using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Infrastructure.Services.FraudService.Responses;

public sealed record FraudCheckResponse(
    [property: JsonPropertyName("authorized")] bool Authorized,
    [property: JsonPropertyName("authorization_code")] string? AuthorizationCode
    );