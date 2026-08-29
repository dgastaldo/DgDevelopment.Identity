namespace DgDevelopment.Identity.Application.Services;

public interface ISessionRevocationService
{
    /// <summary>
    /// Revokes every session and refresh token the user currently has (across every device/client),
    /// then publishes a real-time force-logout event for whichever of them happen to be connected
    /// right now. Called unconditionally on every password change - there's no reliable way to
    /// identify "the session doing the changing" and exclude it (access tokens don't carry a
    /// session id), and forcing every device to re-authenticate after a password change is the
    /// safer default anyway.
    /// </summary>
    Task RevokeAllAsync(Guid userId, CancellationToken ct = default);
}
