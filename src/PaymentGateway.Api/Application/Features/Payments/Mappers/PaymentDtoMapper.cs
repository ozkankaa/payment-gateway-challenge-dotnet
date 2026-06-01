using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Domain.Entities.Payments;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Application.Features.Payments.Mappers;

public static class PaymentDtoMapper
{
    public static PaymentDto ToDto(this Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            Currency = payment.Money.Currency,
            Amount = payment.Money.Amount,
            CardNumberLastFour = int.Parse(payment.CardDetails.LastFour),
            ExpiryMonth = payment.CardDetails.ExpiryMonth,
            ExpiryYear = payment.CardDetails.ExpiryYear,
            Status = payment.Status.ToPaymentStatusEnum()
        };
    }

    private static PaymentStatusEnum ToPaymentStatusEnum(this PaymentStatus status)
    {
        return status switch
        {
            PaymentStatus.Captured => PaymentStatusEnum.Authorized,
            PaymentStatus.Failed => PaymentStatusEnum.Rejected,
            _ => PaymentStatusEnum.Declined
        };
    }
}