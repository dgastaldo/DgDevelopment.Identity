using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.ClientId).IsUnique();

        builder.Property(x => x.ClientId).HasMaxLength(200);
        builder.Property(x => x.ClientSecretHash);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.ClientType).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.RequirePkce);
        builder.Property(x => x.RequireConsent);
        builder.Property(x => x.IsActive);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");

        builder.HasOne<Platform>().WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);

        builder.OwnsMany(x => x.GrantTypes, gt =>
        {
            gt.WithOwner().HasForeignKey("ClientId");
            gt.HasKey("ClientId", "GrantType");
            gt.ToTable("ClientGrantTypes");
            gt.Property(g => g.GrantType).HasMaxLength(50);
        });

        builder.OwnsMany(x => x.Scopes, sc =>
        {
            sc.WithOwner().HasForeignKey("ClientId");
            sc.HasKey("ClientId", "Scope");
            sc.ToTable("ClientScopes");
            sc.Property(s => s.Scope).HasMaxLength(200);
        });

        builder.OwnsMany(x => x.AdminConsentScopes, ac =>
        {
            ac.WithOwner().HasForeignKey("ClientId");
            ac.HasKey("ClientId", "Scope");
            ac.ToTable("ClientAdminConsents");
            ac.Property(s => s.Scope).HasMaxLength(200);
        });

        builder.OwnsMany(x => x.RedirectUris, ru =>
        {
            ru.WithOwner().HasForeignKey("ClientId");
            ru.HasKey("ClientId", nameof(ClientRedirectUri.RedirectUri));
            ru.ToTable("ClientRedirectUris");
            ru.Property(r => r.RedirectUri).HasConversion(v => v.ToString(), v => new Uri(v)).HasMaxLength(2000);
        });

        builder.OwnsMany(x => x.PostLogoutRedirectUris, pr =>
        {
            pr.WithOwner().HasForeignKey("ClientId");
            pr.HasKey("ClientId", nameof(ClientPostLogoutRedirectUri.RedirectUri));
            pr.ToTable("ClientPostLogoutRedirectUris");
            pr.Property(r => r.RedirectUri).HasConversion(v => v.ToString(), v => new Uri(v)).HasMaxLength(2000);
        });
    }
}
