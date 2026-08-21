using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorId);
        builder.Property(x => x.ActorType).HasMaxLength(100);
        builder.Property(x => x.Action).HasMaxLength(200);
        builder.Property(x => x.TargetId).HasMaxLength(100);
        builder.Property(x => x.TargetType).HasMaxLength(100);
        builder.Property(x => x.Details);
        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.Timestamp).HasColumnType("datetime2");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.ActorType);
        builder.HasIndex(x => x.Action);
        builder.HasIndex(x => x.Timestamp);
    }
}
