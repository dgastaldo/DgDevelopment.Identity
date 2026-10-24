using DgDevelopment.Identity.Domain.Repositories;

namespace DgDevelopment.Identity.Application.Services;

public sealed class MfaEnforcementService(
    IUserAuthorizationRepository authorizationRepository,
    IUserRepository userRepository,
    ITotpService totpService,
    IPushMfaService pushMfaService) : IMfaEnforcementService
{
    // Tenant-scoped: a user is only held to this requirement while acting as GlobalAdmin/
    // SuperAdmin in the tenant being logged into, not for every tenant they belong to.
    private static readonly string[] PrivilegedRoleNames = ["GlobalAdmin", "SuperAdmin"];

    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(14);

    public async Task<bool> MustEnrollMfaBeforeProceedingAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var isPrivileged = await authorizationRepository.HasAnyRoleAsync(userId, tenantId, PrivilegedRoleNames, ct).ConfigureAwait(false);
        if (!isPrivileged)
            return false;

        var hasMfaMethod = await totpService.IsEnabledAsync(userId, ct).ConfigureAwait(false)
            || await pushMfaService.HasActiveDevicesAsync(userId, ct).ConfigureAwait(false);
        if (hasMfaMethod)
            return false;

        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user is null)
            return false;

        if (user.MfaGracePeriodStartedAt is null)
        {
            // First time we've seen this user privileged-and-unenrolled: start the clock rather
            // than blocking immediately - see CONTEXT.md item 6 for why (grace period, not a
            // hard cutover).
            user.StartMfaGracePeriodIfNotStarted();
            await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
            return false;
        }

        return DateTime.UtcNow - user.MfaGracePeriodStartedAt.Value >= GracePeriod;
    }
}
