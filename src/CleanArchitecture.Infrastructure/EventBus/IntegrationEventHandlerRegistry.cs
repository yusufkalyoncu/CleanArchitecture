namespace CleanArchitecture.Infrastructure.EventBus;

/// <summary>
/// A singleton registry mapping event types to their concrete handler types.
/// Collected during DI registration to avoid assembly scanning or root provider
/// resolution during dispatch.
/// </summary>
public sealed class IntegrationEventHandlerRegistry
{
    private readonly Dictionary<Type, Type[]> _map = new();

    internal void Register(Type eventType, Type handlerType)
    {
        if (_map.TryGetValue(eventType, out var existing))
            _map[eventType] = [..existing, handlerType];
        else
            _map[eventType] = [handlerType];
    }

    /// <summary>
    /// Returns the registered concrete handler types for the given event type.
    /// </summary>
    public Type[] GetHandlerTypes(Type eventType) =>
        _map.TryGetValue(eventType, out var types) ? types : [];
}