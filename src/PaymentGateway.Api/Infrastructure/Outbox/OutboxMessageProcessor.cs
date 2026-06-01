using Microsoft.EntityFrameworkCore;

using PaymentGateway.Api.Infrastructure.Messaging.Abstraction;
using PaymentGateway.Api.Infrastructure.Persistence;

namespace PaymentGateway.Api.Infrastructure.BackgroundServices;

public sealed class OutboxMessageProcessor(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<OutboxMessageProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessOutboxMessagesAsync(stoppingToken);

            await Task.Delay(Delay, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var publisher =
            scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        var messages = await dbContext.OutboxEvents
            .Where(x => x.ProcessedAtUtc == null)
            .OrderBy(x => x.OccurredAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);

                message.MarkAsProcessed();

                logger.LogInformation(
                    "Outbox message {OutboxMessageId} marked as processed.",
                    message.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to publish outbox message {OutboxMessageId}.",
                    message.Id);

                message.MarkAsFailed(ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}