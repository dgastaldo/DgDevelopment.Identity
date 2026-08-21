using DgDevelopment.Identity.Domain.Authorization;

namespace DgDevelopment.Identity.Application.Authorization;

public interface IPermissionEvaluator
{
    Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permission, string? scopeType = null, string? scopeValue = null, CancellationToken ct = default);
}
