namespace CleanArchitecture.Shared;

/// <summary>
/// Provides the current outbox message ID within the handler's scope.
/// Intended to be used as an idempotency key for inbox/deduplication.
/// </summary>
public interface IMessageContext
{
    Guid MessageId { get; }
}

/// <summary>
/// Populated by the event bus dispatcher per handler scope.
/// </summary>
public sealed class MessageContext : IMessageContext
{
    public Guid MessageId { get; set; }
}