using PaymentGateway.Api.Domain.Exceptions;

namespace PaymentGateway.Api.Domain.Entities.Payments;

public sealed record Money
{
    public long Amount { get; }
    public string Currency { get; }

    private static readonly string[] SupportedCurrencies = ["GBP", "USD", "EUR"];

    private Money(long amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Create(long amount, string currency)
    {
        if (amount < 1)
            throw new DomainValidationException("Amount must be greater than zero.", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainValidationException("Currency is required.", nameof(currency));

        currency = currency.ToUpperInvariant();

        if (currency.Length != 3)
            throw new DomainValidationException("Currency must be 3 characters.", nameof(currency));

        if (!SupportedCurrencies.Contains(currency))
            throw new DomainValidationException("Currency is not supported.", nameof(currency));

        return new Money(amount, currency);
    }
}