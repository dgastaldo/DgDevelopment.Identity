using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class TotpSecretConfiguration : IEntityTypeConfiguration<TotpSecret>
{
    public void Configure(EntityTypeBuilder<TotpSecret> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserId).IsUnique();

        builder.Property(x => x.SecretKey).HasMaxLength(256);
        builder.Property(x => x.IsEnabled);

        builder.HasOne<User>().WithOne().HasForeignKey<TotpSecret>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(x => x.BackupCodes, bc =>
        {
            bc.WithOwner().HasForeignKey("TotpSecretId");
            bc.HasKey("Id");
            bc.ToTable("BackupCodes");
            bc.Property(b => b.CodeHash).HasMaxLength(512);
            bc.Property(b => b.IsUsed);
        });
    }
}
