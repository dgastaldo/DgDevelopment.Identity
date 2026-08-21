using System.Security.Claims;
using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Domain.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DgDevelopment.Identity.Server.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/me")]
public sealed class MeController(ITenantRepository tenantRepository, ITenantContext tenantContext) : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId))
            return Unauthorized();

        var tenants = await tenantRepository.GetTenantsForUserAsync(userId, ct).ConfigureAwait(false);
        var isGlobalAdministrator = await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false);

        return Ok(new MyTenantsResponse(
            tenants.Select(t => new TenantResponse(t.Id, t.Name, t.Slug)).ToList(),
            tenantContext.TenantId,
            isGlobalAdministrator));
    }
}

public sealed record TenantResponse(Guid Id, string Name, string Slug);

public sealed record MyTenantsResponse(IReadOnlyCollection<TenantResponse> Tenants, Guid ActiveTenantId, bool IsGlobalAdministrator);
