namespace PaymentGateway.Api.Domain.Exceptions;

public sealed class DomainValidationException(
    string message,
    string propertyName) : Exception(message)
{
    public string PropertyName { get; } = propertyName;
}
