using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class DeviceCodeConfiguration : IEntityTypeConfiguration<DeviceCode>
{
    public void Configure(EntityTypeBuilder<DeviceCode> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.DeviceCodeHash).IsUnique();
        builder.HasIndex(x => x.UserCodeHash).IsUnique();

        builder.Property(x => x.DeviceCodeHash).HasMaxLength(512);
        builder.Property(x => x.UserCodeHash).HasMaxLength(512);
        builder.Property(x => x.Scopes);
        builder.Property(x => x.IsAuthorized);
        builder.Property(x => x.IsUsed);
        builder.Property(x => x.LastPolledAt).IsRequired(false);

        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasIndex(x => x.TenantId);
    }
}
