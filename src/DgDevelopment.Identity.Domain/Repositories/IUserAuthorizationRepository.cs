using DgDevelopment.Identity.Domain.Authorization;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IUserAuthorizationRepository
{
    Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// True if the user holds any of <paramref name="roleNames"/> in <paramref name="tenantId"/>,
    /// whether assigned directly or inherited through group membership (including nested groups).
    /// </summary>
    Task<bool> HasAnyRoleAsync(Guid userId, Guid tenantId, IReadOnlyCollection<string> roleNames, CancellationToken ct = default);
}
