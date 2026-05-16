using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Models.Responses;

public sealed record BankPaymentResponse(
    [property: JsonPropertyName("authorized")] bool Authorized,
    [property: JsonPropertyName("authorization_code")] string? AuthorizationCode
    );
