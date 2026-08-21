using System.Security.Claims;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;

namespace DgDevelopment.Identity.Server.Services;

public sealed class AuditService(
    IAuditLogRepository repository,
    IHttpContextAccessor httpContextAccessor) : IAuditService
{
    public async Task RecordAsync(string action, AuditOutcome outcome, Guid? actorId = null, string? actorType = null,
        string? targetId = null, string? targetType = null, string? details = null, CancellationToken ct = default)
    {
        var context = httpContextAccessor.HttpContext;
        actorId ??= Guid.TryParse(context?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context?.User.FindFirstValue("sub"), out var parsedActorId) ? parsedActorId : null;

        var entry = new AuditLog(
            action,
            outcome,
            actorId,
            actorType ?? (actorId.HasValue ? "user" : "system"),
            targetId,
            targetType,
            details,
            context?.Connection.RemoteIpAddress?.ToString(),
            context?.Request.Headers.UserAgent.ToString());

        await repository.AddAsync(entry, ct).ConfigureAwait(false);
    }
}
