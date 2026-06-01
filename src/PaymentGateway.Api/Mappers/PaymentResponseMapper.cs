using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Mappers;

public static class PaymentResponseMapper
{
    public static PostPaymentResponse ToResponse(this PaymentDto payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return new PostPaymentResponse
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour.ToString("D4"),
            ExpiryMonth = payment.ExpiryMonth.ToString("D2"),
            ExpiryYear = payment.ExpiryYear.ToString("D4"),
            Currency = payment.Currency,
            Amount = payment.Amount
        };
    }
}