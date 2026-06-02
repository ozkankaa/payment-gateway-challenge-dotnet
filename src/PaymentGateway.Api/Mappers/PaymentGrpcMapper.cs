using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Grpc.V1;

namespace PaymentGateway.Api.Mappers;

public static class PaymentGrpcMapper
{
    public static PaymentGrpcResponse ToGrpcResponse(this PaymentDto payment, string? ErrorCode = null, string? ErrorMessage = null)
    {
        return new PaymentGrpcResponse
        {
            Id = payment.Id.ToString(),
            MerchantId = payment.MerchantId.ToString(),
            Status = payment.Status.ToString(),
            CardNumberLastFour = payment.CardNumberLastFour.ToString("D4"),
            ExpiryMonth = payment.ExpiryMonth.ToString("D2"),
            ExpiryYear = payment.ExpiryYear.ToString("D4"),
            Amount = payment.Amount,
            Currency = payment.Currency,
            ErrorCode = ErrorCode ?? string.Empty,
            ErrorMessage = ErrorMessage ?? string.Empty
        };
    }
}