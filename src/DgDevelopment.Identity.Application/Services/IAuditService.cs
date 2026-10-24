using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Application.Services;

public sealed record AuditEvent(
    string Action, AuditOutcome Outcome, Guid TenantId, Guid? ActorId = null, string? ActorType = null,
    string? TargetId = null, string? TargetType = null, string? Details = null);

public interface IAuditService
{
    Task RecordAsync(AuditEvent auditEvent, CancellationToken ct = default);
}
