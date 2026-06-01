using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;

namespace PaymentGateway.Api.Tests.Application.Features.Payments.ProcessPayment;

public sealed class PaymentFailureFactoryTests
{
    [Fact]
    public void InvalidPaymentRequest_ShouldReturnExpectedError()
    {
        var validationErrors = new Dictionary<string, string[]>
        {
            ["Amount"] = ["Amount is required."],
            ["Currency"] = ["Currency is invalid."]
        };

        var result = PaymentFailureFactory.InvalidPaymentRequest(validationErrors);

        AssertError(
            result,
            expectedCode: "payment_rejected",
            expectedMessage: "Invalid payment request.",
            expectedErrors: validationErrors);
    }

    [Fact]
    public void IdempotencyConflict_ShouldReturnExpectedError()
    {
        const string idempotencyKey = "idem-123";

        var result = PaymentFailureFactory.IdempotencyConflict(idempotencyKey);

        AssertError(
            result,
            expectedCode: "idempotency_conflict",
            expectedMessage: "The Idempotency-Key idem-123 was already used with a different request body.");
    }

    [Fact]
    public void IdempotencyError_WithErrors_ShouldReturnExpectedError()
    {
        const string idempotencyKey = "idem-123";

        var errors = new Dictionary<string, string[]>
        {
            ["IdempotencyKey"] = ["Idempotency-Key header is required."]
        };

        var result = PaymentFailureFactory.IdempotencyError(idempotencyKey, errors);

        AssertError(
            result,
            expectedCode: "idempotency_error",
            expectedMessage: "The idempotency service for Idempotency-Key idem-123 raised an error.",
            expectedErrors: errors);
    }

    [Fact]
    public void IdempotencyError_WithoutErrors_ShouldReturnExpectedError()
    {
        const string idempotencyKey = "idem-123";

        var result = PaymentFailureFactory.IdempotencyError(idempotencyKey, null);

        AssertError(
            result,
            expectedCode: "idempotency_error",
            expectedMessage: "The idempotency service for Idempotency-Key idem-123 raised an error.");
    }

    [Theory]
    [MemberData(nameof(StaticFactoryCases))]
    public void StaticFailures_ShouldReturnExpectedError(
        Func<ErrorDto> factory,
        string expectedCode,
        string expectedMessage)
    {
        var result = factory();

        AssertError(result, expectedCode, expectedMessage);
    }

    public static TheoryData<Func<ErrorDto>, string, string> StaticFactoryCases()
    {
        return new TheoryData<Func<ErrorDto>, string, string>
        {
            {
                PaymentFailureFactory.PaymentDeclinedByFraudService,
                "payment_declined",
                "Fraud service rejected the payment request."
            },
            {
                PaymentFailureFactory.FraudServiceUnavailable,
                "fraud_service_unavailable",
                "Fraud service is unavailable. Try again later."
            },
            {
                PaymentFailureFactory.AcquiringBankRejected,
                "payment_rejected",
                "Acquiring bank rejected the payment request."
            },
            {
                PaymentFailureFactory.AcquiringBankDeclined,
                "payment_declined",
                "Acquiring bank declined the payment request."
            },
            {
                PaymentFailureFactory.BankUnavailable,
                "bank_unavailable",
                "Acquiring bank is unavailable. Try again later."
            },
            {
                PaymentFailureFactory.PaymentPersistenceFailed,
                "payment_failed",
                "Payment could not be stored consistently. Retry with the same Idempotency-Key."
            }
        };
    }

    private static void AssertError(
        ErrorDto result,
        string expectedCode,
        string expectedMessage,
        IDictionary<string, string[]>? expectedErrors = null)
    {
        Assert.NotNull(result);
        Assert.Equal(expectedCode, result.Code);
        Assert.Equal(expectedMessage, result.Message);

        if (expectedErrors is null)
        {
            Assert.Null(result.Errors);
            return;
        }

        Assert.NotNull(result.Errors);
        Assert.Equal(expectedErrors.Count, result.Errors.Count);

        foreach (var expectedError in expectedErrors)
        {
            Assert.True(result.Errors.ContainsKey(expectedError.Key));
            Assert.Equal(expectedError.Value, result.Errors[expectedError.Key]);
        }
    }
}