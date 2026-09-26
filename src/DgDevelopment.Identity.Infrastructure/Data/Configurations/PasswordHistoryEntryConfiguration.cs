using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class PasswordHistoryEntryConfiguration : IEntityTypeConfiguration<PasswordHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PasswordHistoryEntry> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PasswordHash).HasMaxLength(512);

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
