using PaymentGateway.Api.Domain.Exceptions;

namespace PaymentGateway.Api.Domain.Entities.Payments;

public sealed record CardDetails
{
    public string LastFour { get; }
    public int ExpiryMonth { get; }
    public int ExpiryYear { get; }

    private CardDetails(string lastFour, int expiryMonth, int expiryYear)
    {
        LastFour = lastFour;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
    }

    public static CardDetails Create(string lastFour, int expiryMonth, int expiryYear)
    {
        if (string.IsNullOrWhiteSpace(lastFour))
            throw new DomainValidationException("Card number last four digits are required.", nameof(lastFour));

        if (lastFour.Length != 4 || !lastFour.All(char.IsDigit))
            throw new DomainValidationException("Card number last four must contain exactly 4 digits.", nameof(lastFour));

        if (expiryMonth is < 1 or > 12)
            throw new DomainValidationException("Expiry month must be between 1 and 12.", nameof(expiryMonth));

        var now = DateTimeOffset.UtcNow;
        var expiresAt = new DateTimeOffset(expiryYear, expiryMonth, 1, 23, 59, 59, TimeSpan.Zero)
            .AddMonths(1)
            .AddSeconds(-1);

        return expiresAt < now
            ? throw new DomainValidationException("Card has expired.", nameof(expiryYear))
            : new CardDetails(lastFour, expiryMonth, expiryYear);
    }
}