using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Application.Tenants;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Server.Authorization;
using DgDevelopment.Identity.Server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DgDevelopment.Identity.Server.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/tenants")]
public sealed class TenantsController(
    ITenantRepository tenantRepository,
    ITenantProvisioningService provisioningService,
    IAuditService auditService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("identity-platform.tenant.read")]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        var tenants = await tenantRepository.GetAllAsync(ct).ConfigureAwait(false);
        return Ok(tenants.Select(TenantAdminResponse.From));
    }

    [HttpPost]
    [RequirePermission("identity-platform.tenant.create")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var result = await provisioningService.ProvisionAsync(request.Name, request.Slug, ct).ConfigureAwait(false);
            await auditService.RecordAsync("tenant.create", AuditOutcome.Success, result.Tenant.Id, targetId: result.Tenant.Id.ToString(), targetType: "tenant", ct: ct).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetTenants), null, TenantAdminResponse.From(result.Tenant));
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid tenant", detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Tenant already exists", detail: ex.Message);
        }
    }
}
