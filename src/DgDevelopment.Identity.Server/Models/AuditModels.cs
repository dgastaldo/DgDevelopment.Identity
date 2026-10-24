using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Server.Models;

public sealed class AuditLogQueryParameters
{
    public string? ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public string? Action { get; set; }
    public string? TargetId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public bool AllTenants { get; set; }
}

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
