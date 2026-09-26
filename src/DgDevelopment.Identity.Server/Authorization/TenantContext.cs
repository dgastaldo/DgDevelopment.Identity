using System.Security.Claims;
using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace DgDevelopment.Identity.Server.Authorization;

public sealed class TenantContext(
    IHttpContextAccessor httpContextAccessor,
    IPermissionEvaluator permissionEvaluator,
    ITenantRepository tenantRepository) : ITenantContext
{
    private const string GlobalAdministratorPermission = "identity-platform.tenant.read";

    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("No active HTTP context.");

    public Guid TenantId
    {
        get
        {
            var value = User.FindFirstValue("tid");
            return Guid.TryParse(value, out var tenantId)
                ? tenantId
                : throw new InvalidOperationException("The current token does not carry a tenant (tid) claim.");
        }
    }

    public Guid UserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(value, out var userId)
                ? userId
                : throw new InvalidOperationException("The current token does not carry a subject (sub) claim.");
        }
    }

    public Guid? ClientId
    {
        get
        {
            var value = User.FindFirstValue("client_id");
            return Guid.TryParse(value, out var clientId) ? clientId : null;
        }
    }

    public async Task<bool> IsGlobalAdministratorAsync(CancellationToken ct = default)
    {
        // Holding tenant.read alone isn't enough: since the permission catalog is duplicated
        // per tenant, every tenant's own SuperAdmin holds every permission in their own catalog,
        // tenant.read included. Being a genuine global administrator additionally requires that
        // the CURRENT tenant is the one platform tenant, not just any tenant with the permission.
        if (!await permissionEvaluator.HasPermissionAsync(UserId, TenantId, GlobalAdministratorPermission, ct: ct).ConfigureAwait(false))
            return false;

        var tenant = await tenantRepository.GetByIdAsync(TenantId, ct).ConfigureAwait(false);
        return tenant is { IsPlatformTenant: true };
    }
}
