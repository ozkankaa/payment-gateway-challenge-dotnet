using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Mappers;

public static class PostPaymentRequestMapper
{
    public static ProcessPaymentCommand ToCommand(
        this PostPaymentRequest request,
        string? idempotencyKey)
    {
        return new ProcessPaymentCommand(
            MerchantId: request.MerchantId,
            CardNumber: request.CardNumber,
            ExpiryMonth: request.ExpiryMonth,
            ExpiryYear: request.ExpiryYear,
            Currency: request.Currency,
            Amount: request.Amount,
            Cvv: request.Cvv,
            IdempotencyKey: idempotencyKey);
    }
}