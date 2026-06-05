using PaymentGateway.Api.Domain.Exceptions;

namespace PaymentGateway.Api.Domain.Entities.Payments;

public sealed record ProviderReference(string ProviderId, string ProviderToken)
{
    public static ProviderReference Create(string providerId, string providerToken)
    {
        return string.IsNullOrWhiteSpace(providerId)
            ? throw new DomainValidationException("Provider id is required.", nameof(providerId))
            : string.IsNullOrWhiteSpace(providerToken)
            ? throw new DomainValidationException("Provider token is required.", nameof(providerToken))
            : new ProviderReference(providerId, providerToken);
    }
}