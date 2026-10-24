using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Audit;

public sealed class AuditLogService(IAuditLogRepository auditLogRepository) : IAuditLogService
{
    public async Task<PagedResult<AuditLog>> GetPagedAsync(AuditLogFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var items = await auditLogRepository.GetPagedAsync(filter, skip, pageSize, ct).ConfigureAwait(false);
        var total = await auditLogRepository.CountAsync(filter, ct).ConfigureAwait(false);

        return new(items.ToList(), page, pageSize, total);
    }
}
