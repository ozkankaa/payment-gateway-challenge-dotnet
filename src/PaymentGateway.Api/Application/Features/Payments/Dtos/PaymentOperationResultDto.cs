namespace PaymentGateway.Api.Application.Features.Payments.Dtos;

public enum PaymentOperationOutcome
{
    Created,
    Ok,
    NotModified,
    NotFound,
    BadRequest,
    Conflict,
    ServiceUnavailable
}

public sealed record PaymentOperationResultDto()
{
    public PaymentOperationOutcome Outcome { get; set; }
    public PaymentDto? Payment { get; set; }
    public ErrorDto? Error { get; set; }
    public string? ETag { get; set; }    
}