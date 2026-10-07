using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Infrastructure.Inbox;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        builder.HasKey(x => new { x.Consumer, x.MessageId });

        builder.Property(x => x.Consumer).HasMaxLength(300).IsRequired();
        builder.Property(x => x.MessageId).IsRequired();

        builder.Property(x => x.ProcessedOnUtc)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(x => x.ProcessedOnUtc);
    }
}