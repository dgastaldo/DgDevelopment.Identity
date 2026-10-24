using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Audit;

public interface IAuditLogService
{
    Task<PagedResult<AuditLog>> GetPagedAsync(AuditLogFilter filter, int page, int pageSize, CancellationToken ct = default);
}
