using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => new { x.RoleId, x.PermissionId });
        builder.Property(x => x.ScopeType).HasMaxLength(100);
        builder.Property(x => x.ScopeValue).HasMaxLength(200);
    }
}
