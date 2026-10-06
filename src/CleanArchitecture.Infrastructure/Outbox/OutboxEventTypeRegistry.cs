using System.Collections.Frozen;
using System.Reflection;
using System.Text.RegularExpressions;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Infrastructure.Outbox;

/// <summary>
/// Scans assemblies at startup to discover IIntegrationEvent implementations
/// and registers them by their static EventType property.
/// Performs strict validation to prevent startup if types are misconfigured.
/// </summary>
public sealed partial class OutboxEventTypeRegistry
{
    // ^[a-z0-9]+(\.[a-z0-9-]+)+\.v\d+$
    [GeneratedRegex(@"^[a-z0-9]+(\.[a-z0-9-]+)+\.v\d+$", RegexOptions.Compiled)]
    private static partial Regex EventTypeFormatRegex();

    private readonly FrozenDictionary<string, Type> _nameToType;
    private readonly FrozenDictionary<Type, string> _typeToName;

    public OutboxEventTypeRegistry(IEnumerable<Assembly> assemblies)
    {
        var integrationEventType = typeof(IIntegrationEvent);

        var pairs = assemblies
            .Distinct()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException ex)
                {
                    // Ignore unloadable types in some assemblies.
                    return ex.Types.Where(t => t is not null).Cast<Type>();
                }
            })
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && integrationEventType.IsAssignableFrom(t))
            .Select(t =>
            {
                // Read the static property from the concrete type.
                var prop = t.GetProperty("EventType", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                var eventTypeName = prop?.GetValue(null) as string;
                return (Type: t, EventTypeName: eventTypeName);
            })
            .ToList();

        // ── Startup Validations (Fail-Fast) ───────────────────────────────────
        var errors = new List<string>();

        foreach (var (type, name) in pairs)
        {
            if (string.IsNullOrWhiteSpace(name))
                errors.Add($"Type '{type.FullName}' has an empty or null EventType.");
            else if (!EventTypeFormatRegex().IsMatch(name))
                errors.Add($"Type '{type.FullName}' has invalid EventType format: '{name}'. Expected: ^[a-z0-9]+(\\.[a-z0-9-]+)+\\.v\\d+$");
        }

        // Check for duplicate EventType names.
        var duplicates = pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.EventTypeName))
            .GroupBy(p => p.EventTypeName)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var dup in duplicates)
            errors.Add($"Duplicate EventType '{dup.Key}' found on: {string.Join(", ", dup.Select(d => d.Type.FullName))}");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"OutboxEventTypeRegistry startup validation failed:\n{string.Join("\n", errors)}");

        // ── Dictionaries ──────────────────────────────────────────────────────
        var validPairs = pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.EventTypeName))
            .ToList();

        _nameToType = validPairs.ToFrozenDictionary(p => p.EventTypeName!, p => p.Type);
        _typeToName = validPairs.ToFrozenDictionary(p => p.Type, p => p.EventTypeName!);
    }

    /// <summary>Returns the CLR type for a given EventType name, or null if unknown.</summary>
    public Type? Resolve(string eventTypeName) =>
        _nameToType.TryGetValue(eventTypeName, out var t) ? t : null;

    /// <summary>Returns the EventType name for a given CLR type, or throws if unregistered.</summary>
    public string GetName(Type type) =>
        _typeToName.TryGetValue(type, out var name)
            ? name
            : throw new InvalidOperationException(
                $"Type '{type.FullName}' is not registered in OutboxEventTypeRegistry. " +
                "Ensure it implements IIntegrationEvent and has a valid static EventType property.");
}
