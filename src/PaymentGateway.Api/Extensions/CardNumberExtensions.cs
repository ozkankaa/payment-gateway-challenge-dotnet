namespace PaymentGateway.Api.Extensions;

public static class CardNumberExtensions
{
    public static string LastFourDigits(this string? cardNumber)
    {
        return string.IsNullOrWhiteSpace(cardNumber)
            ? string.Empty
            : cardNumber.Length <= 4
            ? cardNumber
            : cardNumber[^4..];
    }
}