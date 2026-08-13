using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => new { x.UserId, x.PermissionId });
        builder.Property(x => x.ScopeType).HasMaxLength(100);
        builder.Property(x => x.ScopeValue).HasMaxLength(200);
    }
}
