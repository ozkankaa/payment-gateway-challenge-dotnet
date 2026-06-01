namespace PaymentGateway.Api.Extensions;

public static class CardNumberExtensions
{
    public static string LastFourDigits(this string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return string.Empty;

        return cardNumber.Length <= 4
            ? cardNumber
            : cardNumber[^4..];
    }
}