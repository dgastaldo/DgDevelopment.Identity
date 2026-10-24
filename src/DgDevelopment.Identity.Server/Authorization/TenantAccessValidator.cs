using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Domain.Repositories;

namespace DgDevelopment.Identity.Server.Authorization;

public sealed class TenantAccessValidator(ITenantContext tenantContext, ITenantRepository tenantRepository) : ITenantAccessValidator
{
    public async Task<bool> CanAccessTenantAsync(Guid requestedTenantId, CancellationToken ct = default)
    {
        if (requestedTenantId == tenantContext.TenantId)
            return true;

        if (!await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false))
            return false;

        var tenant = await tenantRepository.GetByIdAsync(requestedTenantId, ct).ConfigureAwait(false);
        return tenant is { IsActive: true };
    }
}
