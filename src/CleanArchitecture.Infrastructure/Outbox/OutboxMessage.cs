namespace CleanArchitecture.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = null!;
    public string Content { get; init; } = null!;
    public DateTime OccurredOnUtc { get; init; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(string type, string content)
    {
        Id = Guid.NewGuid();
        Type = type;
        Content = content;
        OccurredOnUtc = DateTime.UtcNow;
    }
}