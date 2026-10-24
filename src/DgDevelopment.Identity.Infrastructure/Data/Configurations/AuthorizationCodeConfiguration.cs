using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class AuthorizationCodeConfiguration : IEntityTypeConfiguration<AuthorizationCode>
{
    public void Configure(EntityTypeBuilder<AuthorizationCode> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.CodeHash).IsUnique();

        builder.Property(x => x.CodeHash).HasMaxLength(512);
        builder.Property(x => x.RedirectUri).HasConversion(v => v.ToString(), v => new Uri(v)).HasMaxLength(2000);
        builder.Property(x => x.Scopes);
        builder.Property(x => x.CodeChallengeHash).HasMaxLength(512);
        builder.Property(x => x.CodeChallengeMethod).HasMaxLength(10);
        builder.Property(x => x.IsUsed);

        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.TenantId);
    }
}
