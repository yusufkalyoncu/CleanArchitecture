namespace CleanArchitecture.Domain.Audit;

public sealed class AuditLog
{
    public Guid Id { get; init; } = Guid.NewGuid(); 
    public Guid? UserId { get; init; }
    public string EntityName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public string? ChangedColumns { get; init; }
    public DateTime TimestampUtc { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}