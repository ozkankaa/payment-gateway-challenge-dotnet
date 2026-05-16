using PaymentGateway.Api.Application.Payments.ProcessPayment;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Api.Application.Common;

public static class PaymentRequestHasher
{
    public static string Hash(ProcessPaymentCommand command)
    {
        var normalized = new
        {
            CardNumber = command.CardNumber?.Trim(),
            command.ExpiryMonth,
            command.ExpiryYear,
            Currency = command.Currency?.Trim().ToUpperInvariant(),
            command.Amount,
            Cvv = command.Cvv?.Trim()
        };

        var json = JsonSerializer.Serialize(normalized);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }
}
