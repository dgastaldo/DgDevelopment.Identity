using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Username).IsUnique();

        builder.Property(x => x.Username).HasMaxLength(256);
        builder.Property(x => x.PasswordHash);
        builder.Property(x => x.IsActive);
        builder.Property(x => x.IsLocked);
        builder.Property(x => x.LockoutEnd).HasColumnType("datetime2");
        builder.Property(x => x.FailedLoginAttempts);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");

        builder.OwnsMany(x => x.Emails, email =>
        {
            email.WithOwner().HasForeignKey("UserId");
            email.HasKey("Id");
            email.ToTable("UserEmails");
            email.OwnsOne(e => e.Email, eo =>
            {
                eo.Property(v => v.Value).HasColumnName("Email").HasMaxLength(320);
            });
            email.Property(e => e.IsPrimary);
            email.Property(e => e.IsVerified);
            email.Property(e => e.VerifiedAt).HasColumnType("datetime2");
        });

        builder.OwnsMany(x => x.Claims, claim =>
        {
            claim.WithOwner().HasForeignKey("UserId");
            claim.HasKey("Id");
            claim.ToTable("UserClaims");
            claim.Property(c => c.Type).HasMaxLength(256);
            claim.Property(c => c.Value);
        });

        builder.OwnsMany(x => x.Logins, login =>
        {
            login.WithOwner().HasForeignKey("UserId");
            login.HasKey("Id");
            login.ToTable("UserLogins");
            login.Property(l => l.Provider).HasMaxLength(256);
            login.Property(l => l.ProviderKey).HasMaxLength(512);
            login.Property(l => l.ProviderDisplayName).HasMaxLength(256);
            login.HasIndex(l => new { l.Provider, l.ProviderKey }).IsUnique();
        });

        builder.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Groups).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
