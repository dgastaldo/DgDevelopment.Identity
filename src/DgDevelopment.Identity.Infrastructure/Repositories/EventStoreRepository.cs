using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class EventStoreRepository : IEventStoreRepository
{
    private readonly IdentityDbContext _context;

    public EventStoreRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task AppendAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        await _context.DomainEvents.AddAsync(domainEvent, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<DomainEvent>> GetByAggregateAsync(Guid aggregateId, CancellationToken ct = default)
    {
        return await _context.DomainEvents
            .AsNoTracking()
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.Version)
            .ToListAsync(ct);
    }

    public async Task<int> GetVersionAsync(Guid aggregateId, CancellationToken ct = default)
    {
        var maxVersion = await _context.DomainEvents
            .Where(e => e.AggregateId == aggregateId)
            .MaxAsync(e => (int?)e.Version, ct);

        return maxVersion ?? 0;
    }
}
