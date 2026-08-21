using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Application.Roles;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Server.Authorization;
using DgDevelopment.Identity.Server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DgDevelopment.Identity.Server.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/roles")]
public sealed class RolesController(
    IRoleService roleService,
    IAuditService auditService,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    [RequirePermission("identity-platform.role.read")]
    public async Task<IActionResult> GetRoles([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool allTenants = false, CancellationToken ct = default)
    {
        var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
        var result = await roleService.GetPagedAsync(search, page, pageSize, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
        return Ok(new
        {
            items = result.Items.Select(RoleResponse.From),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("identity-platform.role.read")]
    public async Task<IActionResult> GetRoleById(Guid id, [FromQuery] bool allTenants = false, CancellationToken ct = default)
    {
        var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
        var role = await roleService.GetAsync(id, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
        return role is null ? NotFound() : Ok(RoleResponse.From(role));
    }

    [HttpPost]
    [RequirePermission("identity-platform.role.create")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var role = await roleService.CreateAsync(request.Name, request.Description, tenantContext.TenantId, ct).ConfigureAwait(false);
            await auditService.RecordAsync("role.create", AuditOutcome.Success, tenantContext.TenantId, targetId: role.Id.ToString(), targetType: "role", ct: ct).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetRoleById), new { id = role.Id }, RoleResponse.From(role));
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid role", detail: ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("identity-platform.role.update")]
    public Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "role.update", allTenants, canSeeAllTenants => roleService.UpdateAsync(id, request.Name, request.Description, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission("identity-platform.role.delete")]
    public Task<IActionResult> DeleteRole(Guid id, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "role.delete", allTenants, canSeeAllTenants => roleService.DeleteAsync(id, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpPut("{id:guid}/permissions/{permissionId:guid}")]
    [RequirePermission("identity-platform.role.update")]
    public Task<IActionResult> AssignPermission(Guid id, Guid permissionId, [FromBody] ScopedAssignmentRequest? request, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "role.permission.assign", allTenants, canSeeAllTenants => roleService.AssignPermissionAsync(id, permissionId, request?.ScopeType, request?.ScopeValue, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpDelete("{id:guid}/permissions/{permissionId:guid}")]
    [RequirePermission("identity-platform.role.update")]
    public Task<IActionResult> RemovePermission(Guid id, Guid permissionId, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "role.permission.remove", allTenants, canSeeAllTenants => roleService.RemovePermissionAsync(id, permissionId, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    private async Task<bool> CanSeeAllTenantsAsync(bool requested, CancellationToken ct)
        => requested && await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false);

    private async Task<IActionResult> MutateAsync(Guid id, string action, bool allTenants, Func<bool, Task> mutation, CancellationToken ct)
    {
        try
        {
            var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
            await mutation(canSeeAllTenants).ConfigureAwait(false);
            await auditService.RecordAsync(action, AuditOutcome.Success, tenantContext.TenantId, targetId: id.ToString(), targetType: "role", ct: ct).ConfigureAwait(false);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: ex.Message);
        }
    }
}
