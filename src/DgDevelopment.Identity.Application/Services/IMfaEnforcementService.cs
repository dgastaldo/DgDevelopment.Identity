namespace DgDevelopment.Identity.Application.Services;

public interface IMfaEnforcementService
{
    /// <summary>
    /// True if the user must enroll an MFA method right now before being allowed to proceed
    /// into <paramref name="tenantId"/> - i.e. they hold a privileged role in that tenant, have
    /// no MFA method enrolled, and their enrollment grace period has elapsed. Also responsible
    /// for starting that grace period the first time this situation is observed for a user.
    /// </summary>
    Task<bool> MustEnrollMfaBeforeProceedingAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
}
