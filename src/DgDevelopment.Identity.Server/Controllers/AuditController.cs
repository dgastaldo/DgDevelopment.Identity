using DgDevelopment.Identity.Application.Audit;
using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Server.Authorization;
using DgDevelopment.Identity.Server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DgDevelopment.Identity.Server.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/audit")]
public sealed class AuditController(IAuditLogService auditLogService, ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    [RequirePermission("identity-platform.audit.read")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? actorType, [FromQuery] Guid? actorId, [FromQuery] string? action, [FromQuery] string? targetId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool allTenants = false,
        CancellationToken ct = default)
    {
        var canSeeAllTenants = allTenants && await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false);
        var result = await auditLogService.GetPagedAsync(
            tenantContext.TenantId, canSeeAllTenants, actorType, actorId, action, targetId, from, to, page, pageSize, ct).ConfigureAwait(false);

        return Ok(new
        {
            items = result.Items.Select(AuditLogResponse.From),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages
        });
    }
}
