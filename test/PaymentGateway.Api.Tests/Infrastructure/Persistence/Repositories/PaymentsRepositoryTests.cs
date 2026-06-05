using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using PaymentGateway.Api.Domain.Entities.Payments;
using PaymentGateway.Api.Infrastructure.Persistence;
using PaymentGateway.Api.Infrastructure.Persistence.Repositories;

namespace PaymentGateway.Api.Tests.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PaymentDbContext _dbContext;
    private readonly PaymentRepository _paymentRepository;

    public PaymentRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new PaymentDbContext(options);
        _dbContext.Database.EnsureCreated();

        _paymentRepository = new PaymentRepository(_dbContext);
    }

    [Fact]
    public async Task GetByIdAsync_WhenPaymentExists_ReturnsPayment()
    {
        // Arrange
        var payment = CreatePayment();

        await _dbContext.Payments.AddAsync(payment, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _paymentRepository.GetByIdAsync(payment.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(payment.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenPaymentDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _paymentRepository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_WhenMatchExists_ReturnsPayment()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        const string idempotencyKey = "idem-123";

        var payment = CreatePayment(
            merchantId: merchantId,
            idempotencyKey: idempotencyKey);

        await _dbContext.Payments.AddAsync(payment, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _paymentRepository.GetByIdempotencyKeyAsync(
            merchantId,
            idempotencyKey,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(payment.Id, result.Id);
        Assert.Equal(merchantId, result.MerchantId);
        Assert.Equal(idempotencyKey, result.IdempotencyKey);
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_WhenMerchantDoesNotMatch_ReturnsNull()
    {
        // Arrange
        var payment = CreatePayment(idempotencyKey: "idem-123");

        await _dbContext.Payments.AddAsync(payment, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _paymentRepository.GetByIdempotencyKeyAsync(
            Guid.NewGuid(),
            payment.IdempotencyKey,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_AddsPaymentToDbContext()
    {
        // Arrange
        var payment = CreatePayment();

        // Act
        await _paymentRepository.AddAsync(payment, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var savedPayment = await _dbContext.Payments
            .SingleOrDefaultAsync(x => x.Id == payment.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(savedPayment);
    }

    [Fact]
    public async Task Update_UpdatesExistingPayment()
    {
        // Arrange
        var payment = CreatePayment();

        await _dbContext.Payments.AddAsync(payment, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        payment.MarkAsIdempotencyVerified();
        payment.MarkAsFraudCheckPassed();
        payment.MarkAsAuthorized("acquiring_bank", "acquiring_bank_token");
        payment.MarkAsCaptured();

        // Act
        _paymentRepository.Update(payment);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var updatedPayment = await _dbContext.Payments
            .SingleAsync(x => x.Id == payment.Id, TestContext.Current.CancellationToken);
        Assert.Equal(payment.Status, updatedPayment.Status);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private static Payment CreatePayment(
        Guid? merchantId = null,
        string idempotencyKey = "idem-key")
    {
        return Payment.Create(
            idempotencyKey: idempotencyKey,
            merchantId: merchantId ?? Guid.NewGuid(),
            cardDetails: CardDetails.Create("1234", 10, 2030),
            money: Money.Create(10, "GBP"));
    }
}