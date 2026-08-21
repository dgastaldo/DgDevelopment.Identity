using System.Security.Claims;
using DgDevelopment.Identity.Application.Authorization;
using Microsoft.AspNetCore.Http;

namespace DgDevelopment.Identity.Server.Authorization;

public sealed class TenantContext(IHttpContextAccessor httpContextAccessor, IPermissionEvaluator permissionEvaluator) : ITenantContext
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

    public Task<bool> IsGlobalAdministratorAsync(CancellationToken ct = default)
        => permissionEvaluator.HasPermissionAsync(UserId, TenantId, GlobalAdministratorPermission, ct: ct);
}
