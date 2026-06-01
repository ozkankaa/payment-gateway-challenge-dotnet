using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;

namespace PaymentGateway.Api.Tests.Application.Features.Payments.ProcessPayment;

public sealed class ProcessPaymentExecutionContextTests
{
    [Fact]
    public void Constructor_ShouldInitializeDefaultState()
    {
        var command = CreateValidCommand();

        var context = new ProcessPaymentExecutionContext(command);

        Assert.Same(command, context.Command);
        Assert.True(context.CanContinue);
        Assert.Null(context.Payment);
        Assert.Equal(PaymentOperationOutcome.Ok, context.Result.Outcome);
        Assert.Null(context.Result.Error);
        Assert.NotEmpty(context.RequestHash);
    }

    [Fact]
    public void Constructor_ShouldCreateExpectedRequestHash()
    {
        var command = CreateValidCommand(currency: "gbp");

        var expectedHash = CreateExpectedRequestHash(command);

        var context = new ProcessPaymentExecutionContext(command);

        Assert.Equal(expectedHash, context.RequestHash);
    }

    [Fact]
    public void Constructor_ShouldCreateSameHash_WhenCurrencyCasingDiffers()
    {
        var lowerCaseCommand = CreateValidCommand(currency: "gbp");
        var upperCaseCommand = CreateValidCommand(currency: "GBP");

        var lowerCaseContext = new ProcessPaymentExecutionContext(lowerCaseCommand);
        var upperCaseContext = new ProcessPaymentExecutionContext(upperCaseCommand);

        Assert.Equal(lowerCaseContext.RequestHash, upperCaseContext.RequestHash);
    }

    [Fact]
    public void CreatePayment_WhenCommandIsValid_ShouldCreatePayment()
    {
        var command = CreateValidCommand();

        var context = new ProcessPaymentExecutionContext(command);

        context.CreatePayment();

        Assert.True(context.CanContinue);
        Assert.NotNull(context.Payment);
        Assert.Equal(PaymentOperationOutcome.Ok, context.Result.Outcome);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreatePayment_WhenIdempotencyKeyIsBlank_ShouldStopExecution(string idempotencyKey)
    {
        var command = CreateValidCommand(idempotencyKey: idempotencyKey);

        var context = new ProcessPaymentExecutionContext(command);

        context.CreatePayment();

        Assert.False(context.CanContinue);
        Assert.Null(context.Payment);
        Assert.Equal(PaymentOperationOutcome.BadRequest, context.Result.Outcome);
        Assert.NotNull(context.Result.Error);
    }

    [Fact]
    public void CreatePayment_WhenIdempotencyKeyIsNull_UsesDefaultGuidString()
    {
        var command = CreateValidCommand(idempotencyKey: null);

        var context = new ProcessPaymentExecutionContext(command);

        context.CreatePayment();

        Assert.True(context.CanContinue);
        Assert.NotNull(context.Payment);
    }

    [Fact]
    public void CreatePayment_WhenDomainValidationFails_ShouldStopExecution()
    {
        var command = CreateValidCommand(amount: -10);

        var context = new ProcessPaymentExecutionContext(command);

        context.CreatePayment();

        Assert.False(context.CanContinue);
        Assert.Null(context.Payment);
        Assert.Equal(PaymentOperationOutcome.BadRequest, context.Result.Outcome);
        Assert.NotNull(context.Result.Error);
    }

    [Fact]
    public void StopExecution_WithOutcomeAndError_ShouldStopAndSetResult()
    {
        var command = CreateValidCommand();
        var context = new ProcessPaymentExecutionContext(command);

        var error = new ErrorDto(
            Code : "test_error",
            Message : "Something went wrong"
        );

        context.StopExecution(PaymentOperationOutcome.BadRequest, error);

        Assert.False(context.CanContinue);
        Assert.Equal(PaymentOperationOutcome.BadRequest, context.Result.Outcome);
        Assert.Same(error, context.Result.Error);
    }

    [Fact]
    public void StopExecution_WithResult_ShouldStopAndUseProvidedResult()
    {
        var command = CreateValidCommand();
        var context = new ProcessPaymentExecutionContext(command);

        var result = new PaymentOperationResultDto
        {
            Outcome = PaymentOperationOutcome.BadRequest,
            Error = new ErrorDto(
                Code : "declined",
                Message : "Payment declined"
            )
        };

        context.StopExecution(result);

        Assert.False(context.CanContinue);
        Assert.Same(result, context.Result);
    }

    [Fact]
    public void ContinueExecution_ShouldUpdateResultAndKeepCanContinueTrue()
    {
        var command = CreateValidCommand();
        var context = new ProcessPaymentExecutionContext(command);

        context.ContinueExecution(PaymentOperationOutcome.Ok);

        Assert.True(context.CanContinue);
        Assert.Equal(PaymentOperationOutcome.Ok, context.Result.Outcome);
    }

    private static ProcessPaymentCommand CreateValidCommand(
        string? idempotencyKey = "idem-key",
        Guid? merchantId = null,
        string cardNumber = "4242424242424242",
        int? expiryMonth = 12,
        int? expiryYear = 2030,
        string cvv = "123",
        long? amount = 100,
        string currency = "GBP")
    {
        return new ProcessPaymentCommand(
            IdempotencyKey : idempotencyKey,
            MerchantId : merchantId ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CardNumber : cardNumber,
            ExpiryMonth : expiryMonth,
            ExpiryYear : expiryYear,
            Cvv : cvv,
            Amount : amount,
            Currency : currency
        );
    }

    private static string CreateExpectedRequestHash(ProcessPaymentCommand command)
    {
        var payload = JsonSerializer.Serialize(new
        {
            command.MerchantId,
            command.CardNumber,
            command.ExpiryMonth,
            command.ExpiryYear,
            command.Cvv,
            command.Amount,
            Currency = command.Currency.ToUpperInvariant()
        });

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(bytes);
    }
}