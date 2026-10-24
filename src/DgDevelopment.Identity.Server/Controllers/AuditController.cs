using DgDevelopment.Identity.Application.Audit;
using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Domain.Repositories;
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
    public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogQueryParameters query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var canSeeAllTenants = query.AllTenants && await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false);
        var filter = new AuditLogFilter(
            tenantContext.TenantId, canSeeAllTenants, query.ActorType, query.ActorId, query.Action, query.TargetId, query.From, query.To);
        var result = await auditLogService.GetPagedAsync(filter, query.Page, query.PageSize, ct).ConfigureAwait(false);

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
