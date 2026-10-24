using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public sealed record AuditLogFilter(
    Guid TenantId, bool AllTenants, string? ActorType = null, Guid? ActorId = null,
    string? Action = null, string? TargetId = null, DateTime? FromDate = null, DateTime? ToDate = null);

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct = default);

    Task<IReadOnlyCollection<AuditLog>> GetPagedAsync(AuditLogFilter filter, int skip = 0, int take = 50, CancellationToken ct = default);

    Task<int> CountAsync(AuditLogFilter filter, CancellationToken ct = default);
}
