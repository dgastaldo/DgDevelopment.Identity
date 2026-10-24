using DgDevelopment.Identity.Domain.Authorization;
using DgDevelopment.Identity.Domain.Repositories;

namespace DgDevelopment.Identity.Application.Authorization;

public sealed class EffectivePermissionsService(IUserAuthorizationRepository repository) : IPermissionEvaluator
{
    public Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        => repository.GetEffectivePermissionsAsync(userId, tenantId, ct);

    public async Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permission, string? scopeType = null, string? scopeValue = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var permissions = await GetEffectivePermissionsAsync(userId, tenantId, ct).ConfigureAwait(false);
        return permissions.Any(p => string.Equals(p.Name, permission, StringComparison.Ordinal)
            && (scopeType is null
                || p.ScopeType is null
                || (string.Equals(p.ScopeType, scopeType, StringComparison.Ordinal)
                    && string.Equals(p.ScopeValue, scopeValue, StringComparison.Ordinal))));
    }
}
