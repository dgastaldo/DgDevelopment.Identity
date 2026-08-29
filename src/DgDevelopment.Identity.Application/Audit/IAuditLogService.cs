using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Audit;

public interface IAuditLogService
{
    Task<PagedResult<AuditLog>> GetPagedAsync(Guid tenantId, bool allTenants, string? actorType, Guid? actorId,
        string? action, string? targetId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken ct = default);
}
