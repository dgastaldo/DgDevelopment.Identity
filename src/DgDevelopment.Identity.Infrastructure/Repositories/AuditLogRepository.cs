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

    public async Task<IReadOnlyCollection<AuditLog>> GetPagedAsync(Guid tenantId, bool allTenants, string? actorType = null,
        Guid? actorId = null, string? action = null, string? targetId = null, DateTime? fromDate = null, DateTime? toDate = null,
        int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var query = Filter(tenantId, allTenants, actorType, actorId, action, targetId, fromDate, toDate);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(Guid tenantId, bool allTenants, string? actorType = null, Guid? actorId = null,
        string? action = null, string? targetId = null, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default)
    {
        var query = Filter(tenantId, allTenants, actorType, actorId, action, targetId, fromDate, toDate);
        return await query.CountAsync(ct).ConfigureAwait(false);
    }

    private IQueryable<AuditLog> Filter(Guid tenantId, bool allTenants, string? actorType, Guid? actorId,
        string? action, string? targetId, DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!allTenants)
            query = query.Where(a => a.TenantId == tenantId);

        if (actorType is not null)
            query = query.Where(a => a.ActorType == actorType);

        if (actorId.HasValue)
            query = query.Where(a => a.ActorId == actorId.Value);

        if (action is not null)
            query = query.Where(a => a.Action == action);

        if (targetId is not null)
            query = query.Where(a => a.TargetId == targetId);

        if (fromDate.HasValue)
            query = query.Where(a => a.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.Timestamp <= toDate.Value);

        return query;
    }
}
