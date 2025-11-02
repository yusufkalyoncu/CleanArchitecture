using CleanArchitecture.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Infrastructure.Users;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.Email)
            .HasConversion(
                email => email.Value,
                value => Email.FromValue(value))
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();
        
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");
        
        builder.OwnsOne(u => u.Name, nameBuilder =>
        {
            nameBuilder.Property(n => n.FirstName)
                .HasColumnName("first_name")
                .HasMaxLength(100)
                .IsRequired();
                
            nameBuilder.Property(n => n.LastName)
                .HasColumnName("last_name")
                .HasMaxLength(100)
                .IsRequired();
        });
        
        builder.Property(u => u.Password)
            .HasConversion(
                password => password.HashedValue,
                value => Password.FromHash(value))
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();
    }
}