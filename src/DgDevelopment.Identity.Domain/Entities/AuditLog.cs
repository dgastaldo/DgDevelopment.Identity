namespace DgDevelopment.Identity.Domain.Entities;

public enum AuditOutcome
{
    Success,
    Failure
}

public sealed record AuditActorInfo(Guid? Id = null, string? Type = null);

public sealed record AuditTargetInfo(string? Id = null, string? Type = null);

public sealed record AuditRequestContext(string? IpAddress = null, string? UserAgent = null);

public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? ActorId { get; private set; }
    public string? ActorType { get; private set; }
    public string Action { get; private set; }
    public string? TargetId { get; private set; }
    public string? TargetType { get; private set; }
    public string? Details { get; private set; }
    public AuditOutcome Outcome { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime Timestamp { get; private set; }

    private AuditLog() { }

    public AuditLog(Guid tenantId, string action, AuditOutcome outcome, AuditActorInfo? actor = null,
        AuditTargetInfo? target = null, string? details = null, AuditRequestContext? requestContext = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Action = action;
        Outcome = outcome;
        ActorId = actor?.Id;
        ActorType = actor?.Type;
        TargetId = target?.Id;
        TargetType = target?.Type;
        Details = details;
        IpAddress = requestContext?.IpAddress;
        UserAgent = requestContext?.UserAgent;
        Timestamp = DateTime.UtcNow;
    }
}
