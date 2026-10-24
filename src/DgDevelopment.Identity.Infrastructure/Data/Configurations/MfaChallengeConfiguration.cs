using DgDevelopment.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class MfaChallengeConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.Provider).HasMaxLength(50);
        builder.Property(x => x.ChallengeCodeHash).HasMaxLength(512);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}