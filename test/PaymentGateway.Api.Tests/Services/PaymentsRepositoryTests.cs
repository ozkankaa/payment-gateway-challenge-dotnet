using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Services;

public class PaymentsRepositoryTests
{
    private readonly PaymentsRepository _repository;

    public PaymentsRepositoryTests()
    {
        _repository = new PaymentsRepository();
    }

    [Fact]
    public void AddWithoutIdempotencyKey_ShouldAddPaymentOnly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var payment = new PostPaymentResponse { Id = id };

        // Act
        var added = _repository.TryAdd(payment);

        // Assert
        Assert.True(added);
        var retrieved = _repository.TryGet(id);
        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved!.Id);
    }

    [Fact]
    public void AddWithIdempotencyKey_ShouldAddBoth()
    {
        // Arrange
        var id = Guid.NewGuid();
        var payment = new PostPaymentResponse { Id = id };
        var key = "req-key";
        var hash = "req-hash";

        // Act
        var added = _repository.TryAdd(payment, key, hash);

        // Assert
        Assert.True(added);
        var retrieved = _repository.TryGet(id);
        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved!.Id);

        var retrievedByKey = _repository.TryGetByIdempotencyKey(key);
        Assert.NotNull(retrievedByKey);
        Assert.Equal(id, retrievedByKey!.Payment.Id);
    }

    [Fact]
    public void AddDuplicatePaymentId_ShouldReturnFalseAndNotOverwrite()
    {
        var id = Guid.NewGuid();
        var paymentA = new PostPaymentResponse { Id = id };
        var keyA = "key-A";
        var hashA = "hash-A";

        var addedA = _repository.TryAdd(paymentA, keyA, hashA);

        Assert.True(addedA);

        var paymentB = new PostPaymentResponse { Id = id };
        var keyB = "key-B";
        var hashB = "hash-B";

        var addedB = _repository.TryAdd(paymentB, keyB, hashB);
        Assert.False(addedB);

        var fetched = _repository.TryGet(id);
        Assert.NotNull(fetched);
        Assert.Equal(id, fetched!.Id);

        var idempA = _repository.TryGetByIdempotencyKey(keyA);
        Assert.NotNull(idempA);
        Assert.Equal(id, idempA!.Payment.Id);

        var idempB = _repository.TryGetByIdempotencyKey(keyB);
        Assert.Null(idempB);
    }

    [Fact]
    public void AddWithExistingIdempotencyKey_ShouldRemovePaymentAndReturnFalse()
    {
        var key = "shared-key";

        var idA = Guid.NewGuid();
        var paymentA = new PostPaymentResponse { Id = idA };
        var hashA = "hash-A";

        var addedA = _repository.TryAdd(paymentA, key, hashA);
        Assert.True(addedA);

        var idB = Guid.NewGuid();
        var paymentB = new PostPaymentResponse { Id = idB };
        var hashB = "hash-B";

        var addedB = _repository.TryAdd(paymentB, key, hashB);
        Assert.False(addedB);

        var fetchedA = _repository.TryGet(idA);
        Assert.NotNull(fetchedA);
        Assert.Equal(idA, fetchedA!.Id);

        var fetchedB = _repository.TryGet(idB);
        Assert.Null(fetchedB);

        var idempA = _repository.TryGetByIdempotencyKey(key);
        Assert.NotNull(idempA);
        Assert.Equal(idA, idempA!.Payment.Id);
    }
}
