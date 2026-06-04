using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.PaymentValidation;

namespace PaymentGateway.Api.Tests.Application.Features.Payments.ProcessPayment;

public class PaymentRequestValidatorTests
{
    private readonly ProcessPaymentCommandValidator _validator = new();

    #region Card Number Validations

    [Fact]
    public void Validate_CardNumberIsNull_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: null,
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.CardNumber)));
        Assert.Contains("Card number is required.", errors[nameof(request.CardNumber)]);
    }

    [Fact]
    public void Validate_CardNumberIsEmpty_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: string.Empty,
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.CardNumber)));
        Assert.Contains("Card number is required.", errors[nameof(request.CardNumber)]);
    }

    [Fact]
    public void Validate_CardNumberTooShort_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.CardNumber)));
        Assert.Contains("Card number must be between 13 and 19 characters long.", errors[nameof(request.CardNumber)]);
    }

    [Fact]
    public void Validate_CardNumberTooLong_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "12345678901234567890",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.CardNumber)));
        Assert.Contains("Card number must be between 13 and 19 characters long.", errors[nameof(request.CardNumber)]);
    }

    [Fact]
    public void Validate_CardNumberContainsNonDigits_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123a",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.CardNumber)));
        Assert.Contains("Card number must contain only digits.", errors[nameof(request.CardNumber)]);
    }

    [Theory]
    [InlineData("1234567890123")]
    [InlineData("12345678901234")]
    [InlineData("123456789012345")]
    [InlineData("1234567890123456")]
    [InlineData("12345678901234567")]
    [InlineData("123456789012345678")]
    [InlineData("1234567890123456789")]
    public void Validate_CardNumberValidLength_NoCardNumberError(string cardNumber)
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: cardNumber,
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.False(errors.ContainsKey(nameof(request.CardNumber)) &&
                     errors[nameof(request.CardNumber)].Any(e => e.Contains("length")));
    }

    #endregion

    #region Expiry Month Validations

    [Fact]
    public void Validate_ExpiryMonthIsNull_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: null,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.ExpiryMonth)));
        Assert.Contains("Expiry month is required.", errors[nameof(request.ExpiryMonth)]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    public void Validate_ExpiryMonthOutOfRange_ReturnsError(int month)
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: month,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.ExpiryMonth)));
        Assert.Contains("Expiry month must be between 1 and 12.", errors[nameof(request.ExpiryMonth)]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void Validate_ExpiryMonthValid_NoError(int month)
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: month,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.False(errors.ContainsKey(nameof(request.ExpiryMonth)));
    }

    #endregion

    #region Expiry Year Validations

    [Fact]
    public void Validate_ExpiryYearIsNull_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: null,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.ExpiryYear)));
        Assert.Contains("Expiry year is required.", errors[nameof(request.ExpiryYear)]);
    }

    [Fact]
    public void Validate_ExpiryMonthAndYearInPast_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 1,
            ExpiryYear: 2020,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.ExpiryYear)));
        Assert.Contains("Expiry year must be in the future.", errors[nameof(request.ExpiryYear)]);
    }

    [Fact]
    public void Validate_ExpiryMonthAndYearInFuture_NoError()
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 5;
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: futureYear,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.False(errors.ContainsKey(nameof(request.ExpiryYear)) &&
                     errors[nameof(request.ExpiryYear)].Any(e => e.Contains("future")));
    }

    #endregion

    #region Currency Validations

    [Fact]
    public void Validate_CurrencyIsNull_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.Currency)));
        Assert.Contains("Currency is required.", errors[nameof(request.Currency)]);
    }

    [Fact]
    public void Validate_CurrencyIsEmpty_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: string.Empty,
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.Currency)));
        Assert.Contains("Currency is required.", errors[nameof(request.Currency)]);
    }

    [Theory]
    [InlineData("GB")]
    [InlineData("GBPX")]
    public void Validate_CurrencyWrongLength_ReturnsError(string currency)
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: currency,
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.Currency)));
        Assert.Contains("Currency must be 3 characters.", errors[nameof(request.Currency)]);
    }

    [Fact]
    public void Validate_CurrencyNotSupported_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "JPY",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.Currency)));
        Assert.Contains("Currency is not supported. Supported currencies are GBP, USD and EUR.", errors[nameof(request.Currency)]);
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("gbp")]
    [InlineData("usd")]
    [InlineData("eur")]
    public void Validate_SupportedCurrency_NoError(string currency)
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: currency,
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.False(errors.ContainsKey(nameof(request.Currency)));
    }

    #endregion

    #region Amount Validations

    [Fact]
    public void Validate_AmountIsNull_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: null,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.Amount)));
        Assert.Contains("Amount is required.", errors[nameof(request.Amount)]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(-1)]
    public void Validate_AmountNotPositive_ReturnsError(int amount)
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: amount,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.Amount)));
        Assert.Contains("Amount must be a positive integer in minor currency units.", errors[nameof(request.Amount)]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(999999)]
    public void Validate_AmountPositive_NoError(int amount)
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: amount,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.False(errors.ContainsKey(nameof(request.Amount)));
    }

    #endregion

    #region CVV Validations

    [Fact]
    public void Validate_CvvIsNull_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: null,
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.Cvv)));
        Assert.Contains("CVV is required.", errors[nameof(request.Cvv)]);
    }

    [Fact]
    public void Validate_CvvIsEmpty_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: string.Empty,
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.Cvv)));
        Assert.Contains("CVV is required.", errors[nameof(request.Cvv)]);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    public void Validate_CvvWrongLength_ReturnsError(string cvv)
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: cvv,
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.Cvv)));
        Assert.Contains("CVV must be 3-4 characters long.", errors[nameof(request.Cvv)]);
    }

    [Fact]
    public void Validate_CvvContainsNonDigits_ReturnsError()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "12a",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.ContainsKey(nameof(request.Cvv)));
        Assert.Contains("CVV must contain numeric characters only.", errors[nameof(request.Cvv)]);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    public void Validate_CvvValid_NoError(string cvv)
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: 2025,
            Currency: "GBP",
            Amount: 1000,
            Cvv: cvv,
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.False(errors.ContainsKey(nameof(request.Cvv)));
    }

    #endregion

    #region Integration tests

    [Fact]
    public void Validate_AllFieldsValid_NoErrors()
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 1;
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "1234567890123",
            ExpiryMonth: 12,
            ExpiryYear: futureYear,
            Currency: "GBP",
            Amount: 1000,
            Cvv: "123",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MultipleFieldsInvalid_ReturnsMultipleErrors()
    {
        // Arrange
        var request = new ProcessPaymentCommand
        (
            MerchantId: Guid.NewGuid(),
            CardNumber: "123",
            ExpiryMonth: 13,
            ExpiryYear: 2020,
            Currency: "JPY",
            Amount: 0,
            Cvv: "12",
            IdempotencyKey: "B653A7F8-1DF6-4F6E-B6DD-02376127E43B"
        );

        // Act
        var errors = _validator.Validate(request);

        // Assert
        Assert.True(errors.Count >= 5);
        Assert.True(errors.ContainsKey(nameof(request.CardNumber)));
        Assert.True(errors.ContainsKey(nameof(request.ExpiryMonth)));
        Assert.True(errors.ContainsKey(nameof(request.ExpiryYear)));
        Assert.True(errors.ContainsKey(nameof(request.Currency)));
        Assert.True(errors.ContainsKey(nameof(request.Amount)));
        Assert.True(errors.ContainsKey(nameof(request.Cvv)));
    }

    #endregion
}
