using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Models.Requests;

public sealed record PostPaymentRequest(
    [Required] string CardNumber,
    [Required] int? ExpiryMonth,
    [Required] int? ExpiryYear,
    [Required] string Currency,
    [Required] long? Amount,
    [Required] string Cvv);