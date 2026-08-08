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
        await _context.AuditLogs.AddAsync(entry, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<AuditLog>> GetAsync(string? actorType = null, Guid? actorId = null,
        string? action = null, DateTime? from = null, DateTime? endDate = null, int skip = 0, int take = 50,
        CancellationToken ct = default)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (actorType is not null)
            query = query.Where(a => a.ActorType == actorType);

        if (actorId.HasValue)
            query = query.Where(a => a.ActorId == actorId.Value);

        if (action is not null)
            query = query.Where(a => a.Action == action);

        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.Timestamp <= endDate.Value);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }
}
