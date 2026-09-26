using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class PlatformConfiguration : IEntityTypeConfiguration<Platform>
{
    public void Configure(EntityTypeBuilder<Platform> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.PermissionMode).HasConversion<string>().HasMaxLength(50);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.TenantId);
    }
}
