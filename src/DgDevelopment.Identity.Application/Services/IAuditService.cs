using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Application.Services;

public interface IAuditService
{
    Task RecordAsync(string action, AuditOutcome outcome, Guid tenantId, Guid? actorId = null, string? actorType = null,
        string? targetId = null, string? targetType = null, string? details = null, CancellationToken ct = default);
}
