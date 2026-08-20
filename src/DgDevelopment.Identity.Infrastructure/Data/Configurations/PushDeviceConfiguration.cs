using DgDevelopment.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.UserId, x.IsActive });

        builder.Property(x => x.Platform).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.PushToken).HasMaxLength(512);
        builder.Property(x => x.DeviceName).HasMaxLength(256);
        builder.Property(x => x.IsActive);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.LastSeenAt).HasColumnType("datetime2");

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}