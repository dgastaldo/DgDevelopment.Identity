using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class VerificationTokenConfiguration : IEntityTypeConfiguration<VerificationToken>
{
    public void Configure(EntityTypeBuilder<VerificationToken> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.Property(x => x.TokenHash).HasMaxLength(512);
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.TargetEmail).HasMaxLength(320);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.ExpiresAt).HasColumnType("datetime2");

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UserId);
    }
}
