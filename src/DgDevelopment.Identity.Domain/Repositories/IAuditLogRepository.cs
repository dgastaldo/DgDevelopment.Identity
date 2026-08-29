using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct = default);

    Task<IReadOnlyCollection<AuditLog>> GetPagedAsync(Guid tenantId, bool allTenants, string? actorType = null,
        Guid? actorId = null, string? action = null, string? targetId = null, DateTime? fromDate = null, DateTime? toDate = null,
        int skip = 0, int take = 50, CancellationToken ct = default);

    Task<int> CountAsync(Guid tenantId, bool allTenants, string? actorType = null, Guid? actorId = null,
        string? action = null, string? targetId = null, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default);
}
