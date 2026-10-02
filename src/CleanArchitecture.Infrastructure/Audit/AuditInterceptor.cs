using System.Reflection;
using System.Text.Json;
using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        if (eventData.Context is null) 
            return base.SavingChangesAsync(eventData, result, cancellationToken);

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
            if (!IsAuditable(entry)) continue;

            auditLogs.Add(CreateAuditLog(entry));
        }

        return auditLogs;
    }

    private static bool IsAuditable(EntityEntry entry)
    {
        return entry is { Entity: IAuditable, State: not EntityState.Detached and not EntityState.Unchanged };
    }

    private readonly record struct AuditContext(
        Dictionary<string, object?> OldValues,
        Dictionary<string, object?> NewValues,
        List<string> ChangedColumns);

    private AuditLog CreateAuditLog(EntityEntry entry)
    {
        var context = new AuditContext(new(), new(), new());

        ProcessProperties(entry, context);
        ProcessComplexProperties(entry, context);

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userContext.Id != Guid.Empty ? userContext.Id : null,
            EntityName = entry.Metadata.ClrType.Name,
            Action = entry.State.ToString(),
            TimestampUtc = DateTime.UtcNow,
            IpAddress = userContext.IpAddress,
            UserAgent = userContext.UserAgent,
            OldValues = context.OldValues.Count > 0 ? JsonSerializer.Serialize(context.OldValues) : null,
            NewValues = context.NewValues.Count > 0 ? JsonSerializer.Serialize(context.NewValues) : null,
            ChangedColumns = context.ChangedColumns.Count > 0 ? string.Join(", ", context.ChangedColumns) : null
        };
    }

    private static void ProcessProperties(EntityEntry entry, AuditContext context)
    {
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            
            if (property.Metadata.IsPrimaryKey())
            {
                context.NewValues[name] = property.CurrentValue;
                continue;
            }

            bool isMasked = property.Metadata.PropertyInfo?.GetCustomAttribute<AuditMaskAttribute>() is not null;
            ProcessProperty(entry.State, property.IsModified, name, property.OriginalValue, property.CurrentValue, isMasked, context);
        }
    }

    private static void ProcessComplexProperties(EntityEntry entry, AuditContext context)
    {
        foreach (var complexProperty in entry.ComplexProperties)
        {
            foreach (var prop in complexProperty.Properties)
            {
                var name = $"{complexProperty.Metadata.Name}_{prop.Metadata.Name}";
                bool isMasked = prop.Metadata.PropertyInfo?.GetCustomAttribute<AuditMaskAttribute>() is not null;

                ProcessProperty(entry.State, prop.IsModified, name, prop.OriginalValue, prop.CurrentValue, isMasked, context);
            }
        }
    }

    private static void ProcessProperty(
        EntityState state, bool isModified, string name, object? original, object? current, bool isMasked,
        AuditContext context)
    {
        var originalFormatted = FormatValue(original, isMasked);
        var currentFormatted = FormatValue(current, isMasked);

        switch (state)
        {
            case EntityState.Added:
                context.NewValues[name] = currentFormatted;
                break;
            case EntityState.Deleted:
                context.OldValues[name] = originalFormatted;
                break;
            case EntityState.Modified when isModified:
                context.ChangedColumns.Add(name);
                context.OldValues[name] = originalFormatted;
                context.NewValues[name] = currentFormatted;
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