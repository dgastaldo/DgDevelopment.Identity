namespace DgDevelopment.Identity.Domain.Entities;

public enum AuditOutcome
{
    Success,
    Failure
}

public sealed class AuditLog
{
    public Guid Id { get; private set; }
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

    public AuditLog(string action, AuditOutcome outcome, Guid? actorId = null, string? actorType = null,
        string? targetId = null, string? targetType = null, string? details = null,
        string? ipAddress = null, string? userAgent = null)
    {
        Id = Guid.NewGuid();
        Action = action;
        Outcome = outcome;
        ActorId = actorId;
        ActorType = actorType;
        TargetId = targetId;
        TargetType = targetType;
        Details = details;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Timestamp = DateTime.UtcNow;
    }
}
