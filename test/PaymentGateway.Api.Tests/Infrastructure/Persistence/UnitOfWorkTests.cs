using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Moq;

using PaymentGateway.Api.Application.Features.Payments.DomainEvents;
using PaymentGateway.Api.Domain.Abstractions;
using PaymentGateway.Api.Domain.Entities.Payments;
using PaymentGateway.Api.Infrastructure.Persistence;

namespace PaymentGateway.Api.Tests.Infrastructure.Persistence;

public sealed class UnitOfWorkTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PaymentDbContext _dbContext;
    private readonly Mock<IDomainEventHandler> _domainEventHandlerMock;
    private readonly UnitOfWork _unitOfWork;

    public UnitOfWorkTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new PaymentDbContext(options);
        _dbContext.Database.EnsureCreated();

        _domainEventHandlerMock = new Mock<IDomainEventHandler>();

        _unitOfWork = new UnitOfWork(
            _dbContext,
            _domainEventHandlerMock.Object);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenAggregateHasDomainEvents_HandlesDomainEvents()
    {
        // Arrange
        var domainEvent = new TestDomainEvent();

        var payment = CreatePayment();
        payment.RaiseTestDomainEvent(domainEvent);

        _dbContext.Payments.Add(payment);

        // Act
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        _domainEventHandlerMock.Verify(
            x => x.HandleAsync(domainEvent, It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Empty(payment.DomainEvents);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenAggregateHasDomainEvents_ClearsDomainEvents()
    {
        // Arrange
        var payment = CreatePayment();
        var domainEvent = new TestDomainEvent();
        payment.RaiseTestDomainEvent(domainEvent);
        _dbContext.Payments.Add(payment);

        // Act
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(payment.DomainEvents);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenMultipleDomainEventsExist_HandlesAllEvents()
    {
        // Arrange
        var payment = CreatePayment();
        var domainEvent = new TestDomainEvent();

        payment.RaiseTestDomainEvent(domainEvent);
        payment.RaiseTestDomainEvent(domainEvent);

        _dbContext.Payments.Add(payment);

        // Act
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        _domainEventHandlerMock.Verify(
            x => x.HandleAsync(
                It.IsAny<IDomainEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task SaveChangesAsync_WhenNoDomainEventsExist_DoesNotCallHandler()
    {
        // Arrange
        var payment = CreatePayment();
        _dbContext.Payments.Add(payment);

        // Act
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        _domainEventHandlerMock.Verify(
            x => x.HandleAsync(
                It.IsAny<IDomainEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(1));
    }

    [Fact]
    public async Task SaveChangesAsync_ReturnsNumberOfWrittenEntries()
    {
        // Arrange
        var payment = CreatePayment();

        _dbContext.Payments.Add(payment);

        // Act
        var result = await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task SaveChangesAsync_PassesCancellationTokenToDomainEventHandler()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        var payment = CreatePayment();
        var domainEvent = new TestDomainEvent();

        payment.RaiseTestDomainEvent(domainEvent);

        _dbContext.Payments.Add(payment);        

        // Act
        await _unitOfWork.SaveChangesAsync(cancellationTokenSource.Token);

        // Assert
        _domainEventHandlerMock.Verify(
            x => x.HandleAsync(domainEvent, cancellationTokenSource.Token),
            Times.Once);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private static Payment CreatePayment()
    {
        return Payment.Create(
            idempotencyKey: Guid.NewGuid().ToString(),
            merchantId: Guid.NewGuid(),
            cardDetails: CardDetails.Create("1234", 10, 2030),
            money: Money.Create(10, "GBP"));
    }

    private sealed class TestAggregateRoot : AggregateRoot
    {
        public Guid Id { get; private set; } = Guid.NewGuid();

        public void AddDomainEvent(IDomainEvent domainEvent)
        {
            RaiseDomainEvent(domainEvent);
        }
    }

    private sealed record TestDomainEvent : IDomainEvent
    {
        public Guid Id => Guid.NewGuid();

        public DateTime OccurredAtUtc => DateTime.UtcNow;
    }
}