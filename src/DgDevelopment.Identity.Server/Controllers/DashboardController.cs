using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Server.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DgDevelopment.Identity.Server.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/dashboard")]
public sealed class DashboardController(IUserRepository userRepository, ITenantContext tenantContext) : ControllerBase
{
    [HttpGet("summary")]
    [RequirePermission("identity-platform.dashboard.read")]
    public async Task<IActionResult> GetSummary([FromQuery] bool allTenants, CancellationToken ct)
    {
        var canSeeAllTenants = allTenants && await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false);
        var users = await userRepository.GetStatisticsAsync(tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
        return Ok(new
        {
            users.Total,
            users.Active,
            users.Locked,
            users.CreatedLast30Days,
            users.MfaEnabled
        });
    }
}
