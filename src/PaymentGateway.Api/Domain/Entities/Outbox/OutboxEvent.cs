namespace PaymentGateway.Api.Domain.Entities.Outbox;

public sealed class OutboxEvent : IEntity
{
    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public DateTime OccurredAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public string? Error { get; private set; }

    private OutboxEvent()
    {
    }

    public static OutboxEvent Create(string type, string content)
    {
        var outboxEvent = new OutboxEvent()
        { 
            Id = Guid.NewGuid(),
            Type = type,
            Content = content,
            OccurredAtUtc = DateTime.UtcNow
        };
        return outboxEvent;
    }

    public OutboxEvent(
        Guid id,
        string type,
        string content,
        DateTime occurredAtUtc)
    {
        Id = id;
        Type = type;
        Content = content;
        OccurredAtUtc = occurredAtUtc;
    }

    public void MarkAsProcessed()
    {
        ProcessedAtUtc = DateTime.UtcNow;
        Error = null;
    }

    public void MarkAsFailed(string error)
    {
        Error = error;
    }
}