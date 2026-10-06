namespace CleanArchitecture.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = null!;
    public string Content { get; init; } = null!;
    public DateTime OccurredOnUtc { get; init; }
    public DateTime? ProcessedOnUtc { get; init; }
    public string? Error { get; init; }
    public int RetryCount { get; init; }
    public DateTime? NextAttemptAtUtc { get; init; }
    public DateTime? LockedUntilUtc { get; init; }
    public DateTime? DeadLetteredOnUtc { get; init; }

    private OutboxMessage() { }

    public OutboxMessage(string type, string content)
    {
        Id = Guid.NewGuid();
        Type = type;
        Content = content;
        OccurredOnUtc = DateTime.UtcNow;
    }
}