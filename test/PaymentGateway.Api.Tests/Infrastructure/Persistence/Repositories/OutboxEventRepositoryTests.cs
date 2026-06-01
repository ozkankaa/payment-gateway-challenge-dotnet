using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using PaymentGateway.Api.Domain.Entities.Outbox;
using PaymentGateway.Api.Infrastructure.Persistence;
using PaymentGateway.Api.Infrastructure.Persistence.Repositories;

namespace PaymentGateway.Api.Tests.Infrastructure.Persistence.Repositories;

public sealed class OutboxEventRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PaymentDbContext _dbContext;
    private readonly OutboxEventRepository _outboxEventRepository;

    public OutboxEventRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new PaymentDbContext(options);
        _dbContext.Database.EnsureCreated();

        _outboxEventRepository = new OutboxEventRepository(_dbContext);
    }

    [Fact]
    public async Task AddAsync_ShouldAddOutboxEventToDbContext()
    {
        // Arrange
        var outboxEvent = CreateOutboxEvent();

        // Act
        await _outboxEventRepository.AddAsync(outboxEvent, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedEvent = await _dbContext.OutboxEvents
            .SingleOrDefaultAsync(x => x.Id == outboxEvent.Id);

        Assert.NotNull(savedEvent);
        Assert.Equal(outboxEvent.Id, savedEvent.Id);
        Assert.Equal(outboxEvent.Type, savedEvent.Type);
        Assert.Equal(outboxEvent.Content, savedEvent.Content);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private static OutboxEvent CreateOutboxEvent()
    {
        return OutboxEvent.Create(
            type: "PaymentCreated",
            content: """
            {
                "paymentId": "7f73f2a1-8f85-44aa-9215-96bbf2dbad2a"
            }
            """);
    }
}