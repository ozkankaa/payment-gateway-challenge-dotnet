using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Infrastructure.Services.FraudService.Requests;

public sealed record FraudCheckRequest([property: JsonPropertyName("card_number")] string CardNumber);
