using PaymentGateway.Api.Domain.Entities.Payments;
using PaymentGateway.Api.Domain.Exceptions;

namespace PaymentGateway.Api.Tests.Domain.Entities.Payments;

public sealed class CardDetailsTests
{
    [Fact]
    public void Create_WhenValidDetails_ReturnsCardDetails()
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 1;

        // Act
        var result = CardDetails.Create("1234", 12, futureYear);

        // Assert
        Assert.Equal("1234", result.LastFour);
        Assert.Equal(12, result.ExpiryMonth);
        Assert.Equal(futureYear, result.ExpiryYear);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WhenLastFourIsMissing_ThrowsDomainValidationException(
        string? lastFour)
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 1;

        // Act
        var exception = Assert.Throws<DomainValidationException>(() =>
            CardDetails.Create(lastFour!, 12, futureYear));

        // Assert
        Assert.Equal("lastFour", exception.PropertyName);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12A4")]
    [InlineData("ABCD")]
    public void Create_WhenLastFourIsInvalid_ThrowsDomainValidationException(
        string lastFour)
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 1;

        // Act
        var exception = Assert.Throws<DomainValidationException>(() =>
            CardDetails.Create(lastFour, 12, futureYear));

        // Assert
        Assert.Equal("lastFour", exception.PropertyName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Create_WhenExpiryMonthIsInvalid_ThrowsDomainValidationException(
        int expiryMonth)
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 1;

        // Act
        var exception = Assert.Throws<DomainValidationException>(() =>
            CardDetails.Create("1234", expiryMonth, futureYear));

        // Assert
        Assert.Equal("expiryMonth", exception.PropertyName);
    }

    [Fact]
    public void Create_WhenCardIsExpired_ThrowsDomainValidationException()
    {
        // Arrange
        var previousYear = DateTimeOffset.UtcNow.Year - 1;

        // Act
        var exception = Assert.Throws<DomainValidationException>(() =>
            CardDetails.Create("1234", 12, previousYear));

        // Assert
        Assert.Equal("expiryYear", exception.PropertyName);
    }

    [Fact]
    public void Create_WhenCardExpiresThisMonth_ReturnsCardDetails()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = CardDetails.Create("1234", now.Month, now.Year);

        // Assert
        Assert.Equal("1234", result.LastFour);
        Assert.Equal(now.Month, result.ExpiryMonth);
        Assert.Equal(now.Year, result.ExpiryYear);
    }
}