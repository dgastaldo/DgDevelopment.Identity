using DgDevelopment.Identity.Domain.Authorization;

namespace DgDevelopment.Identity.Application.Authorization;

public interface IPermissionEvaluator
{
    Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(Guid userId, string permission, string? scopeType = null, string? scopeValue = null, CancellationToken ct = default);
}
