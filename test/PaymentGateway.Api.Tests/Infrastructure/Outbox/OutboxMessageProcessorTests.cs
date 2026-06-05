using System.Reflection;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Domain.Entities.Outbox;
using PaymentGateway.Api.Infrastructure.Outbox;
using PaymentGateway.Api.Infrastructure.Messaging.Abstraction;
using PaymentGateway.Api.Infrastructure.Persistence;

namespace PaymentGateway.Api.Tests.Infrastructure.Outbox;

public sealed class OutboxMessageProcessorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IIntegrationEventPublisher> _publisherMock = new();
    private readonly Mock<ILogger<OutboxMessageProcessor>> _loggerMock = new();
    private readonly OutboxMessageProcessor _sut;

    public OutboxMessageProcessorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setupContext = CreateDbContext();
        setupContext.Database.EnsureCreated();

        var services = new ServiceCollection();

        services.AddDbContext<PaymentDbContext>(options => options.UseSqlite(_connection));

        services.AddScoped(_ => _publisherMock.Object);

        _serviceProvider = services.BuildServiceProvider();

        _sut = new OutboxMessageProcessor(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenMessageIsUnprocessed_PublishesAndMarksAsProcessed()
    {
        // Arrange
        var message = CreateOutboxMessage();

        await using (var context = CreateDbContext())
        {
            await context.OutboxEvents.AddAsync(message, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await InvokeProcessOutboxMessagesAsync();

        // Assert
        _publisherMock.Verify(
            x => x.PublishAsync(
                It.Is<OutboxEvent>(m => m.Id == message.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);

        await using var assertContext = CreateDbContext();

        var persisted = await assertContext.OutboxEvents
            .SingleAsync(x => x.Id == message.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(persisted.ProcessedAtUtc);
        Assert.Null(persisted.Error);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenMessageAlreadyProcessed_DoesNotPublish()
    {
        // Arrange
        var message = CreateOutboxMessage();
        message.MarkAsProcessed();

        await using (var context = CreateDbContext())
        {
            await context.OutboxEvents.AddAsync(message, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await InvokeProcessOutboxMessagesAsync();

        // Assert
        _publisherMock.Verify(
            x => x.PublishAsync(
                It.IsAny<OutboxEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenPublishFails_MarksMessageAsFailed()
    {
        // Arrange
        var message = CreateOutboxMessage();

        await using (var context = CreateDbContext())
        {
            await context.OutboxEvents.AddAsync(message, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        _publisherMock
            .Setup(x => x.PublishAsync(
                It.IsAny<OutboxEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Broker unavailable"));

        // Act
        await InvokeProcessOutboxMessagesAsync();

        // Assert
        var persisted = await CreateDbContext()
            .OutboxEvents
            .SingleAsync(x => x.Id == message.Id, TestContext.Current.CancellationToken);

        Assert.Null(persisted.ProcessedAtUtc);
        Assert.Contains("Broker unavailable", persisted.Error);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenMoreThanTwentyMessagesExist_ProcessesOnlyTwenty()
    {
        // Arrange
        var baseDate = DateTime.UtcNow;

        var messages = Enumerable
            .Range(1, 25)
            .Select(i => CreateOutboxMessage(baseDate.AddMinutes(i)))
            .ToList();

        await using (var context = CreateDbContext())
        {
            await context.OutboxEvents.AddRangeAsync(messages, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await InvokeProcessOutboxMessagesAsync();

        // Assert
        _publisherMock.Verify(
            x => x.PublishAsync(
                It.IsAny<OutboxEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(20));

        await using var assertContext = CreateDbContext();

        var processedCount = await assertContext.OutboxEvents
            .CountAsync(x => x.ProcessedAtUtc != null, TestContext.Current.CancellationToken);

        Assert.Equal(20, processedCount);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_ProcessesMessagesInOccurredAtUtcOrder()
    {
        // Arrange
        var baseDate = DateTime.UtcNow;

        var latest = CreateOutboxMessage(baseDate.AddMinutes(3));
        var earliest = CreateOutboxMessage(baseDate.AddMinutes(1));
        var middle = CreateOutboxMessage(baseDate.AddMinutes(2));

        await using (var context = CreateDbContext())
        {
            await context.OutboxEvents.AddRangeAsync(latest, earliest, middle);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var publishedIds = new List<Guid>();

        _publisherMock
            .Setup(x => x.PublishAsync(
                It.IsAny<OutboxEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<OutboxEvent, CancellationToken>((message, _) => publishedIds.Add(message.Id))
            .Returns(Task.CompletedTask);

        // Act
        await InvokeProcessOutboxMessagesAsync();

        // Assert
        Assert.Equal(
            new[] { earliest.Id, middle.Id, latest.Id },
            publishedIds);
    }

    private async Task InvokeProcessOutboxMessagesAsync()
    {
        var method = typeof(OutboxMessageProcessor).GetMethod(
            "ProcessOutboxMessagesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = (Task)method.Invoke(
            _sut,
            [CancellationToken.None])!;

        await task;
    }

    private PaymentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new PaymentDbContext(options);
    }

    private static OutboxEvent CreateOutboxMessage(DateTime? occurredAtUtc = null)
    {
        return new OutboxEvent
        (
            id : Guid.NewGuid(),
            occurredAtUtc : occurredAtUtc ?? DateTime.UtcNow,
            type : "PaymentCreated",
            content : """
            {
              "paymentId": "00000000-0000-0000-0000-000000000001"
            }
            """
        );
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }
}