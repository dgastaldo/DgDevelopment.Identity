using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Audit;

public sealed class AuditLogService(IAuditLogRepository auditLogRepository) : IAuditLogService
{
    public async Task<PagedResult<AuditLog>> GetPagedAsync(Guid tenantId, bool allTenants, string? actorType, Guid? actorId,
        string? action, string? targetId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var items = await auditLogRepository.GetPagedAsync(tenantId, allTenants, actorType, actorId, action, targetId, fromDate, toDate, skip, pageSize, ct).ConfigureAwait(false);
        var total = await auditLogRepository.CountAsync(tenantId, allTenants, actorType, actorId, action, targetId, fromDate, toDate, ct).ConfigureAwait(false);

        return new(items.ToList(), page, pageSize, total);
    }
}
