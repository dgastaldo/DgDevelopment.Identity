using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly IdentityDbContext _context;

    public AuditLogRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog entry, CancellationToken ct = default)
    {
        await _context.AuditLogs.AddAsync(entry, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<AuditLog>> GetPagedAsync(AuditLogFilter filter, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var query = Filter(filter);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(AuditLogFilter filter, CancellationToken ct = default)
    {
        var query = Filter(filter);
        return await query.CountAsync(ct).ConfigureAwait(false);
    }

    private IQueryable<AuditLog> Filter(AuditLogFilter filter)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!filter.AllTenants)
            query = query.Where(a => a.TenantId == filter.TenantId);

        if (filter.ActorType is not null)
            query = query.Where(a => a.ActorType == filter.ActorType);

        if (filter.ActorId.HasValue)
            query = query.Where(a => a.ActorId == filter.ActorId.Value);

        if (filter.Action is not null)
            query = query.Where(a => a.Action == filter.Action);

        if (filter.TargetId is not null)
            query = query.Where(a => a.TargetId == filter.TargetId);

        if (filter.FromDate.HasValue)
            query = query.Where(a => a.Timestamp >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(a => a.Timestamp <= filter.ToDate.Value);

        return query;
    }
}
