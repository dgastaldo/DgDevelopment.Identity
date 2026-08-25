using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Server.Models;

public sealed record AuditLogResponse(
    Guid Id,
    Guid TenantId,
    Guid? ActorId,
    string? ActorType,
    string Action,
    string? TargetId,
    string? TargetType,
    string? Details,
    AuditOutcome Outcome,
    string? IpAddress,
    string? UserAgent,
    DateTime Timestamp)
{
    public static AuditLogResponse From(AuditLog entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new(entry.Id, entry.TenantId, entry.ActorId, entry.ActorType, entry.Action, entry.TargetId,
            entry.TargetType, entry.Details, entry.Outcome, entry.IpAddress, entry.UserAgent, entry.Timestamp);
    }
}
