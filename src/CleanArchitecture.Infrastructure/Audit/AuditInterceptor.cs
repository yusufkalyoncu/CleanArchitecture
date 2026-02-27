using System.Reflection;
using System.Text.Json;
using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleanArchitecture.Infrastructure.Audit;

public sealed class AuditInterceptor(IUserContext userContext) : SaveChangesInterceptor
{
    private const string MaskedValue = "***";

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        var auditEntries = CaptureAuditDetails(eventData.Context);

        if (auditEntries.Count != 0)
        {
            eventData.Context.Set<AuditLog>().AddRange(auditEntries);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private List<AuditLog> CaptureAuditDetails(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var auditLogs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not IAuditable || entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();
            var changedColumns = new List<string>();

            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey())
                {
                    newValues[name] = property.CurrentValue;
                    continue;
                }

                bool isMasked = property.Metadata.PropertyInfo?.GetCustomAttribute<AuditMaskAttribute>() is not null;
                ProcessProperty(entry.State, property.IsModified, name, property.OriginalValue, property.CurrentValue, isMasked, oldValues, newValues, changedColumns);
            }

            foreach (var complexProperty in entry.ComplexProperties)
            {
                foreach (var prop in complexProperty.Properties)
                {
                    var name = $"{complexProperty.Metadata.Name}_{prop.Metadata.Name}";
                    bool isMasked = prop.Metadata.PropertyInfo?.GetCustomAttribute<AuditMaskAttribute>() is not null;

                    ProcessProperty(entry.State, prop.IsModified, name, prop.OriginalValue, prop.CurrentValue, isMasked, oldValues, newValues, changedColumns);
                }
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userContext.Id != Guid.Empty ? userContext.Id : null,
                EntityName = entry.Metadata.ClrType.Name,
                Action = entry.State.ToString(),
                TimestampUtc = DateTime.UtcNow,
                IpAddress = userContext.IpAddress,
                UserAgent = userContext.UserAgent,
                OldValues = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null,
                ChangedColumns = changedColumns.Count > 0 ? string.Join(", ", changedColumns) : null
            };

            auditLogs.Add(auditLog);
        }

        return auditLogs;
    }

    private static void ProcessProperty(
        EntityState state, bool isModified, string name, object? original, object? current, bool isMasked,
        Dictionary<string, object?> oldValues, Dictionary<string, object?> newValues, List<string> changedColumns)
    {
        var originalFormatted = FormatValue(original, isMasked);
        var currentFormatted = FormatValue(current, isMasked);

        switch (state)
        {
            case EntityState.Added:
                newValues[name] = currentFormatted;
                break;

            case EntityState.Deleted:
                oldValues[name] = originalFormatted;
                break;

            case EntityState.Modified:
                if (isModified)
                {
                    changedColumns.Add(name);
                    oldValues[name] = originalFormatted;
                    newValues[name] = currentFormatted;
                }
                break;
        }
    }

    private static object? FormatValue(object? value, bool isMasked)
    {
        if (value is null) return null;
        if (isMasked) return MaskedValue;

        var type = value.GetType();
        if (type is { IsValueType: true, IsPrimitive: false } && type != typeof(Guid) && type != typeof(DateTime))
        {
            return value.ToString();
        }

        return value;
    }
}