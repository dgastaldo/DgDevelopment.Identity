using System.Security.Claims;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;

namespace DgDevelopment.Identity.Server.Services;

public sealed class AuditService(
    IAuditLogRepository repository,
    IHttpContextAccessor httpContextAccessor) : IAuditService
{
    public async Task RecordAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var context = httpContextAccessor.HttpContext;
        var actorId = auditEvent.ActorId ?? (Guid.TryParse(context?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context?.User.FindFirstValue("sub"), out var parsedActorId) ? parsedActorId : null);

        var entry = new AuditLog(
            auditEvent.TenantId,
            auditEvent.Action,
            auditEvent.Outcome,
            new AuditActorInfo(actorId, auditEvent.ActorType ?? (actorId.HasValue ? "user" : "system")),
            new AuditTargetInfo(auditEvent.TargetId, auditEvent.TargetType),
            auditEvent.Details,
            new AuditRequestContext(context?.Connection.RemoteIpAddress?.ToString(), context?.Request.Headers.UserAgent.ToString()));

        await repository.AddAsync(entry, ct).ConfigureAwait(false);
    }
}
