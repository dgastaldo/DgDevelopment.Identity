using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.Property(x => x.TokenHash).HasMaxLength(512);
        builder.Property(x => x.Scopes);
        builder.Property(x => x.PreviousTokenId);
        builder.Property(x => x.IsRevoked);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.ExpiresAt).HasColumnType("datetime2");

        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.PreviousTokenId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.TenantId);
    }
}
