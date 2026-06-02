using Grpc.Core;

using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.GetPayment;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Grpc.V1;
using PaymentGateway.Api.Mappers;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Grpc;

public sealed class PaymentGrpcV1Service(
    GetPaymentHandler getPaymentHandler,
    ProcessPaymentHandler processPaymentHandler,
    ILogger<PaymentGrpcV1Service> logger)
    : PaymentsGrpc.PaymentsGrpcBase
{
    public override async Task<PaymentGrpcResponse> GetPayment(
        GetPaymentGrpcRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var paymentId))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Invalid payment id."));
        }

        logger.LogInformation("Retrieving payment {PaymentId} via gRPC.", paymentId);

        var result = await getPaymentHandler.HandleAsync(
            new GetPaymentQuery(paymentId),
            context.CancellationToken);

        return result.Outcome switch
        {
            PaymentOperationOutcome.Ok => result.Payment!.ToGrpcResponse(),

            PaymentOperationOutcome.NotFound => throw new RpcException(new Status(
                StatusCode.NotFound,
                "Payment was not found.")),

            _ => throw new RpcException(new Status(
                StatusCode.Internal,
                "Unexpected error while retrieving payment."))
        };
    }

    public override async Task<PaymentGrpcResponse> ProcessPayment(
        ProcessPaymentGrpcRequest request,
        ServerCallContext context)
    {
        logger.LogInformation(
            "Processing payment for merchant {MerchantId} via gRPC.",
            request.MerchantId);

        if (!Guid.TryParse(request.MerchantId, out var merchantId))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Invalid merchant id."));
        }

        var postPaymentRequest = new PostPaymentRequest
        (
            MerchantId : merchantId,
            Amount : request.Amount,
            Currency : request.Currency,
            CardNumber : request.CardNumber,
            ExpiryMonth : request.ExpiryMonth,
            ExpiryYear : request.ExpiryYear,
            Cvv : request.Cvv
        );

        var command = postPaymentRequest.ToCommand(request.IdempotencyKey);

        var result = await processPaymentHandler.HandleAsync(
            command,
            context.CancellationToken);

        return result.Outcome switch
        {
            PaymentOperationOutcome.Created or PaymentOperationOutcome.Ok =>
                result.Payment!.ToGrpcResponse(),

            PaymentOperationOutcome.BadRequest => throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error?.Message ?? "Invalid payment request.")),

            PaymentOperationOutcome.Conflict => throw new RpcException(new Status(
                StatusCode.AlreadyExists,
                result.Error?.Message ?? "Payment conflict.")),

            PaymentOperationOutcome.ServiceUnavailable => throw new RpcException(new Status(
                StatusCode.Unavailable,
                result.Error?.Message ?? "Payment provider unavailable.")),

            _ => throw new RpcException(new Status(
                StatusCode.Internal,
                "Unexpected error while processing payment."))
        };
    }
}