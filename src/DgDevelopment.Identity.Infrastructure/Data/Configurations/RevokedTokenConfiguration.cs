using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class RevokedTokenConfiguration : IEntityTypeConfiguration<RevokedToken>
{
    public void Configure(EntityTypeBuilder<RevokedToken> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.JtiHash).IsUnique();

        builder.Property(x => x.JtiHash).HasMaxLength(512);
        builder.Property(x => x.TokenType).HasMaxLength(50);
        builder.Property(x => x.ClientId);
        builder.Property(x => x.UserId);
    }
}