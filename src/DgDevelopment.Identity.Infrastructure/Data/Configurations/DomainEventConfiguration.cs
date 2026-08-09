using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Infrastructure.Data.Configurations;

public sealed class DomainEventConfiguration : IEntityTypeConfiguration<DomainEvent>
{
    public void Configure(EntityTypeBuilder<DomainEvent> builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AggregateId);
        builder.Property(x => x.AggregateType).HasMaxLength(200);
        builder.Property(x => x.EventType).HasMaxLength(200);
        builder.Property(x => x.Data);
        builder.Property(x => x.Version);
        builder.Property(x => x.Timestamp).HasColumnType("datetime2");

        builder.HasIndex(x => x.AggregateId);
        builder.HasIndex(x => x.AggregateType);
    }
}
