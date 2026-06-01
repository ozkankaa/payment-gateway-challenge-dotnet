using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Infrastructure.Services.AcquiringBankService.Requests;

public sealed record BankPaymentRequest(
    [property: JsonPropertyName("card_number")] string CardNumber,
    [property: JsonPropertyName("expiry_date")] string ExpiryDate,
    [property: JsonPropertyName("cvv")] string Cvv,
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("currency")] string Currency
    );

