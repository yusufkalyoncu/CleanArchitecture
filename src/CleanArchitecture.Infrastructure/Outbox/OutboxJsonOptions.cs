using System.Text.Json;

namespace CleanArchitecture.Infrastructure.Outbox;

/// <summary>
/// Shared JSON serialization options for both OutboxService (write) and OutboxProcessor (read).
/// Essential for preventing deserialization mismatch errors.
/// </summary>
public static class OutboxJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}