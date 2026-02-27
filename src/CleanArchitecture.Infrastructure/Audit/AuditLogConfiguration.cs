using CleanArchitecture.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Infrastructure.Audit;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired(false);

        builder.Property(x => x.EntityName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Action)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.OldValues)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(x => x.NewValues)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(x => x.ChangedColumns)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(x => x.TimestampUtc)
            .IsRequired();

        builder.Property(x => x.IpAddress)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(512)
            .IsRequired(false);

        builder.HasIndex(x => x.EntityName);
        builder.HasIndex(x => x.TimestampUtc);
        builder.HasIndex(x => x.UserId);
    }
}