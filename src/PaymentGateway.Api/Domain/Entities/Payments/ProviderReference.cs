using PaymentGateway.Api.Domain.Exceptions;

namespace PaymentGateway.Api.Domain.Entities.Payments;

public sealed record ProviderReference(string ProviderId, string ProviderToken)
{
    public static ProviderReference Create(string providerId, string providerToken)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new DomainValidationException("Provider id is required.", nameof(providerId));

        if (string.IsNullOrWhiteSpace(providerToken))
            throw new DomainValidationException("Provider token is required.", nameof(providerToken));

        return new ProviderReference(providerId, providerToken);
    }
}