namespace PaymentGateway.Api.Application.Features.Payments.Dtos;

public sealed record ErrorDto(
string Code,
string Message,
IDictionary<string, string[]>? Errors = null)
{
    public override string ToString()
    {
        var result = $"{Code} : {Message}";
        if (Errors != null && Errors.Count > 0)
            result += $" -> {string.Join(',', Errors)}";
        return result;
    }
};
