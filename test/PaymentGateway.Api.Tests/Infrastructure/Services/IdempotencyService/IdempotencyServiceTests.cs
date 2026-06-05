using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;

namespace PaymentGateway.Api.Tests.Infrastructure.Services.IdempotencyService;

public sealed class IdempotencyServiceTests
{
    private readonly Api.Infrastructure.Services.IdempotencyService.IdempotencyService _idempotencyService = new();

    [Fact]
    public void TryAdd_WhenKeyAndHashAreValid_ReturnsAdded()
    {
        // Arrange
        var key = NewKey();
        var hash = "hash-1";

        // Act
        var result = _idempotencyService.TryAdd(key, hash);

        // Assert
        Assert.Equal(IdempotencyStatus.Added, result.Status);
        Assert.Null(result.Payment);
        Assert.Equal(hash, result.RequestHash);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("", "hash")]
    [InlineData(" ", "hash")]
    [InlineData("key", "")]
    [InlineData("key", " ")]
    [InlineData("", "")]
    public void TryAdd_WhenInputIsInvalid_ReturnsValidationError(
        string key,
        string hash)
    {
        // Act
        var result = _idempotencyService.TryAdd(key, hash);

        // Assert
        Assert.Equal(IdempotencyStatus.Error, result.Status);
        Assert.Equal(hash, result.RequestHash);
        Assert.NotNull(result.Error);
        Assert.Equal("idempotency_validation_error", result.Error.Code);
    }

    [Fact]
    public void TryAdd_WhenSameKeyAndSameHashAlreadyAdded_ReturnsExistingAdded()
    {
        // Arrange
        var key = NewKey();
        var hash = "hash-1";

        var first = _idempotencyService.TryAdd(key, hash);

        // Act
        var second = _idempotencyService.TryAdd(key, hash);

        // Assert
        Assert.Equal(IdempotencyStatus.Added, first.Status);
        Assert.Equal(IdempotencyStatus.Added, second.Status);
        Assert.Equal(first.RequestHash, second.RequestHash);
        Assert.Null(second.Error);
    }

    [Fact]
    public void TryAdd_WhenSameKeyButDifferentHash_ReturnsConflict()
    {
        // Arrange
        var key = NewKey();

        _idempotencyService.TryAdd(key, "hash-1");

        // Act
        var result = _idempotencyService.TryAdd(key, "hash-2");

        // Assert
        Assert.Equal(IdempotencyStatus.Conflict, result.Status);
        Assert.Equal("hash-2", result.RequestHash);
        Assert.NotNull(result.Error);
        Assert.Equal("idempotency_conflict", result.Error.Code);
    }

    [Fact]
    public void TryGet_WhenKeyExists_ReturnsStoredResult()
    {
        // Arrange
        var key = NewKey();
        var hash = "hash-1";

        _idempotencyService.TryAdd(key, hash);

        // Act
        var result = _idempotencyService.TryGet(key);

        // Assert
        Assert.Equal(IdempotencyStatus.Added, result.Status);
        Assert.Equal(hash, result.RequestHash);
        Assert.Null(result.Error);
    }

    [Fact]
    public void TryGet_WhenKeyDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        var key = NewKey();

        // Act
        var result = _idempotencyService.TryGet(key);

        // Assert
        Assert.Equal(IdempotencyStatus.Error, result.Status);
        Assert.Null(result.Payment);
        Assert.Null(result.RequestHash);
        Assert.NotNull(result.Error);
        Assert.Equal("idempotency_notfound", result.Error.Code);
    }

    [Fact]
    public void TryUpdate_WhenExistingKeyAndMatchingHash_ReturnsUpdated()
    {
        // Arrange
        var key = NewKey();
        var hash = "hash-1";
        var payment = CreatePaymentDto();

        _idempotencyService.TryAdd(key, hash);

        // Act
        var result = _idempotencyService.TryUpdate(payment, key, hash);

        // Assert
        Assert.Equal(IdempotencyStatus.Updated, result.Status);
        Assert.Equal(hash, result.RequestHash);
        Assert.Equal(payment, result.Payment);
        Assert.Null(result.Error);
    }

    [Fact]
    public void TryUpdate_WhenKeyDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        var key = NewKey();
        var payment = CreatePaymentDto();

        // Act
        var result = _idempotencyService.TryUpdate(payment, key, "hash-1");

        // Assert
        Assert.Equal(IdempotencyStatus.Error, result.Status);
        Assert.Null(result.Payment);
        Assert.Null(result.RequestHash);
        Assert.NotNull(result.Error);
        Assert.Equal("idempotency_notfound", result.Error.Code);
    }

    [Fact]
    public void TryUpdate_WhenHashDoesNotMatch_ReturnsConflict()
    {
        // Arrange
        var key = NewKey();
        var payment = CreatePaymentDto();

        _idempotencyService.TryAdd(key, "hash-1");

        // Act
        var result = _idempotencyService.TryUpdate(payment, key, "hash-2");

        // Assert
        Assert.Equal(IdempotencyStatus.Conflict, result.Status);
        Assert.Equal(payment, result.Payment);
        Assert.Equal("hash-2", result.RequestHash);
        Assert.NotNull(result.Error);
        Assert.Equal("idempotency_conflict", result.Error.Code);
    }

    [Theory]
    [InlineData("", "hash")]
    [InlineData("key", "")]
    [InlineData(" ", " ")]
    public void TryUpdate_WhenInputIsInvalid_ReturnsValidationError(
        string key,
        string hash)
    {
        // Arrange
        var payment = CreatePaymentDto();

        // Act
        var result = _idempotencyService.TryUpdate(payment, key, hash);

        // Assert
        Assert.Equal(IdempotencyStatus.Error, result.Status);
        Assert.Equal(payment, result.Payment);
        Assert.Equal(hash, result.RequestHash);
        Assert.NotNull(result.Error);
        Assert.Equal("idempotency_validation_error", result.Error.Code);
    }

    [Fact]
    public void TryAdd_WhenExistingEntryAlreadyUpdatedWithSameHash_ReturnsDuplicate()
    {
        // Arrange
        var key = NewKey();
        var hash = "hash-1";
        var payment = CreatePaymentDto();

        _idempotencyService.TryAdd(key, hash);
        _idempotencyService.TryUpdate(payment, key, hash);

        // Act
        var result = _idempotencyService.TryAdd(key, hash);

        // Assert
        Assert.Equal(IdempotencyStatus.Duplicate, result.Status);
        Assert.Equal(payment, result.Payment);
        Assert.Equal(hash, result.RequestHash);
        Assert.NotNull(result.Error);
        Assert.Equal("idempotency_exists", result.Error.Code);
    }

    private static string NewKey()
    {
        return $"idem-{Guid.NewGuid():N}";
    }

    private static PaymentDto CreatePaymentDto()
    {        
        return new PaymentDto()
        {
            Id = Guid.NewGuid(),
            Status = Models.PaymentStatusEnum.Authorized
        };
    }
}