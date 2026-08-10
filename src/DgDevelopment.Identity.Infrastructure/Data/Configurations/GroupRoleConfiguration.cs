using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class GroupRoleConfiguration : IEntityTypeConfiguration<GroupRole>
{
    public void Configure(EntityTypeBuilder<GroupRole> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => new { x.GroupId, x.RoleId });
        builder.Property(x => x.ScopeType).HasMaxLength(100);
        builder.Property(x => x.ScopeValue).HasMaxLength(200);
    }
}
