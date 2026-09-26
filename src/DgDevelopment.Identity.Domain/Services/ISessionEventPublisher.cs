namespace DgDevelopment.Identity.Domain.Services;

/// <summary>
/// Pushes session-lifecycle events to whichever clients are currently listening for a given user
/// - a real-time notification, not a durable log. Defined here (Domain) rather than Application so
/// an Application-layer orchestrator (e.g. session revocation on password change) can depend on
/// it directly, the same way it already depends on IPasswordHasher/INotificationService; the real
/// implementation (SignalR) lives in Server, the one project that already hosts a hub (MfaHub) and
/// references Microsoft.AspNetCore.SignalR.
/// </summary>
public interface ISessionEventPublisher
{
    /// <summary>
    /// Tells any client currently connected and subscribed for this user that their sessions have
    /// been revoked (password changed, admin-initiated revocation, etc.) and they should treat
    /// themselves as logged out. A no-op if nothing is currently listening - the passive database
    /// revocation this always runs alongside is what guarantees the outcome either way.
    /// </summary>
    Task PublishForceLogoutAsync(Guid userId, CancellationToken ct = default);
}
