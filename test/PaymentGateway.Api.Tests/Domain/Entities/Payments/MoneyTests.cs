using PaymentGateway.Api.Domain.Entities.Payments;
using PaymentGateway.Api.Domain.Exceptions;

namespace PaymentGateway.Api.Tests.Domain.Entities.Payments;

public sealed class MoneyTests
{
    [Theory]
    [InlineData(1, "GBP")]
    [InlineData(100, "USD")]
    [InlineData(9999, "EUR")]
    public void Create_WhenValid_ReturnsMoney(long amount, string currency)
    {
        var money = Money.Create(amount, currency);

        Assert.Equal(amount, money.Amount);
        Assert.Equal(currency, money.Currency);
    }

    [Theory]
    [InlineData("gbp", "GBP")]
    [InlineData("usd", "USD")]
    [InlineData("eur", "EUR")]
    [InlineData("GbP", "GBP")]
    public void Create_WhenCurrencyHasDifferentCasing_NormalizesToUppercase(
        string currency,
        string expectedCurrency)
    {
        var money = Money.Create(100, currency);

        Assert.Equal(expectedCurrency, money.Currency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WhenAmountIsLessThanOne_ThrowsDomainValidationException(long amount)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => Money.Create(amount, "GBP"));

        Assert.Equal("Amount must be greater than zero.", exception.Message);
        Assert.Equal("amount", exception.PropertyName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Create_WhenCurrencyIsMissing_ThrowsDomainValidationException(string? currency)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => Money.Create(100, currency!));

        Assert.Equal("Currency is required.", exception.Message);
        Assert.Equal("currency", exception.PropertyName);
    }

    [Theory]
    [InlineData("GB")]
    [InlineData("GBPP")]
    [InlineData("US")]
    [InlineData("EURO")]
    public void Create_WhenCurrencyLengthIsNotThree_ThrowsDomainValidationException(string currency)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => Money.Create(100, currency));

        Assert.Equal("Currency must be 3 characters.", exception.Message);
        Assert.Equal("currency", exception.PropertyName);
    }

    [Theory]
    [InlineData("CAD")]
    [InlineData("AUD")]
    [InlineData("JPY")]
    public void Create_WhenCurrencyIsNotSupported_ThrowsDomainValidationException(string currency)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => Money.Create(100, currency));

        Assert.Equal("Currency is not supported.", exception.Message);
        Assert.Equal("currency", exception.PropertyName);
    }

    [Fact]
    public void Money_WhenValuesAreSame_AreEqual()
    {
        var first = Money.Create(100, "GBP");
        var second = Money.Create(100, "GBP");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Money_WhenValuesAreDifferent_AreNotEqual()
    {
        var first = Money.Create(100, "GBP");
        var second = Money.Create(200, "GBP");

        Assert.NotEqual(first, second);
    }
}