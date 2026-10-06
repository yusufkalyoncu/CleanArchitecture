namespace CleanArchitecture.Shared;

public interface IIntegrationEvent
{
    /// <summary>
    /// A stable event identifier that is independent of the class name.
    /// <para>
    /// Format: <c>^[a-z0-9]+(\.[a-z0-9-]+)+\.v\d+$</c> (e.g., "identity.user-registered.v1")
    /// </para>
    /// <para>
    /// <c>OutboxEventTypeRegistry</c> validates this value at startup.
    /// </para>
    /// </summary>
    static abstract string EventType { get; }
}