using PaymentGateway.Api.Application.Payments.ProcessPayment;

namespace PaymentGateway.Api.Services;

public class PaymentRequestValidator : IPaymentRequestValidator
{
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBP", "USD", "EUR"
    };

    public IDictionary<string, string[]> Validate(ProcessPaymentCommand request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                errors[field] = messages = [];
            }
            messages.Add(message);
        }

        // Card number validations
        if (string.IsNullOrWhiteSpace(request.CardNumber))
        {
            Add(nameof(request.CardNumber), "Card number is required.");
        }
        else
        {
            if (request.CardNumber.Length is < 13 or > 19)
            {
                Add(nameof(request.CardNumber), "Card number must be between 13 and 19 characters long.");
            }
            if (!request.CardNumber.All(char.IsDigit))
            {
                Add(nameof(request.CardNumber), "Card number must contain only digits.");
            }
        }

        // Expiry month validations
        if (request.ExpiryMonth is null)
        {
            Add(nameof(request.ExpiryMonth), "Expiry month is required.");
        }
        else
        {
            if (request.ExpiryMonth is < 1 or > 12)
            {
                Add(nameof(request.ExpiryMonth), "Expiry month must be between 1 and 12.");
            }
        }

        // Expiry year validations
        if (request.ExpiryYear is null)
        {
            Add(nameof(request.ExpiryYear), "Expiry year is required.");
        }
        else
        {
            if (request.ExpiryYear < DateTimeOffset.UtcNow.Year)
            {
                Add(nameof(request.ExpiryYear), "Expiry year must be in the future.");
            }
        }

        if (request.ExpiryMonth is >= 1 and <= 12 && request.ExpiryYear is not null && request.ExpiryYear >= DateTimeOffset.UtcNow.Year)
        {
            var now = DateTimeOffset.UtcNow;
            var expiresAtEndOfMonth = new DateTimeOffset(request.ExpiryYear.Value, request.ExpiryMonth.Value, 1, 23, 59, 59, TimeSpan.Zero)
                .AddMonths(1).AddSeconds(-1);
            if (expiresAtEndOfMonth < now) Add(nameof(request.ExpiryYear), "Expiry year must be in the future.");
        }

        // Currency validations
        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            Add(nameof(request.Currency), "Currency is required.");
        }
        else
        {
            if (request.Currency.Length != 3)
            {
                Add(nameof(request.Currency), "Currency must be 3 characters.");

            }
            if (!SupportedCurrencies.Contains(request.Currency))
            {
                Add(nameof(request.Currency), "Currency is not supported. Supported currencies are GBP, USD and EUR.");
            }
        }

        // Amount validations
        if (request.Amount is null)
        {
            Add(nameof(request.Amount), "Amount is required.");
        }
        else if (request.Amount <= 0)
        {
            Add(nameof(request.Amount), "Amount must be a positive integer in minor currency units.");
        }

        // CVV validations
        if (string.IsNullOrWhiteSpace(request.Cvv))
        {
            Add(nameof(request.Cvv), "CVV is required.");
        }
        else
        {
            if (request.Cvv.Length is < 3 or > 4)
            {
                Add(nameof(request.Cvv), "CVV must be 3-4 characters long.");
            }
            if (!request.Cvv.All(char.IsDigit))
            {
                Add(nameof(request.Cvv), "CVV must contain numeric characters only.");
            }
        }

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }
}
