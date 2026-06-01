namespace PaymentGateway.Api.Domain.Exceptions;

public sealed class DomainValidationException : Exception
{
    public string PropertyName { get; }

    public DomainValidationException(        
        string message, 
        string propertyName)
        : base(message)
    {
        PropertyName = propertyName;
    }
}
