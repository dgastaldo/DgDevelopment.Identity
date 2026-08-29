using DgDevelopment.Identity.Application.Authorization;
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

// Tenant.read/.create are IsGlobal permissions (see TenantProvisioningService's standard catalog),
// but every tenant's own SuperAdmin holds them within their own tenant - RequirePermission alone
// only checks "does this user have this permission in their current tenant", not "is this
// permission being exercised globally". Without the explicit IsGlobalAdministratorAsync check
// below, any tenant's SuperAdmin could list or create tenants system-wide.
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/tenants")]
public sealed class TenantsController(
    ITenantRepository tenantRepository,
    ITenantProvisioningService provisioningService,
    ITenantContext tenantContext,
    IAuditService auditService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("identity-platform.tenant.read")]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        if (!await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false))
            return Forbid();

        var tenants = await tenantRepository.GetAllAsync(ct).ConfigureAwait(false);
        return Ok(tenants.Select(TenantAdminResponse.From));
    }

    [HttpPost]
    [RequirePermission("identity-platform.tenant.create")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false))
            return Forbid();

        try
        {
            var result = await provisioningService.ProvisionAsync(request.Name, request.Slug, ct: ct).ConfigureAwait(false);
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

    [HttpPut("{id:guid}/active")]
    [RequirePermission("identity-platform.tenant.update")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetTenantActiveRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false))
            return Forbid();

        var tenant = await tenantRepository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (tenant is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Tenant not found");

        // The platform tenant owns the system itself - deactivating it would lock out every
        // administrator, including whoever would need to reactivate it.
        if (tenant.IsPlatformTenant)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Cannot deactivate the platform tenant",
                detail: "The platform tenant cannot be deactivated.");

        if (request.IsActive) tenant.Activate(); else tenant.Deactivate();
        await tenantRepository.UpdateAsync(tenant, ct).ConfigureAwait(false);

        await auditService.RecordAsync(request.IsActive ? "tenant.activate" : "tenant.deactivate", AuditOutcome.Success,
            tenant.Id, targetId: tenant.Id.ToString(), targetType: "tenant", ct: ct).ConfigureAwait(false);
        return Ok(TenantAdminResponse.From(tenant));
    }
}
