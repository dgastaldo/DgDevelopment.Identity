using DgDevelopment.Identity.Domain.Authorization;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IUserAuthorizationRepository
{
    Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
}
