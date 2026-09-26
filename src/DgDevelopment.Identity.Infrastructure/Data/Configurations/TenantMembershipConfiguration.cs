using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.IsOwner);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
