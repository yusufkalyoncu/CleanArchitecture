namespace CleanArchitecture.Infrastructure.Inbox;

public sealed class InboxMessage
{
    public string Consumer { get; init; } = null!;
    public Guid MessageId { get; init; }
    public DateTimeOffset ProcessedOnUtc { get; init; }
}