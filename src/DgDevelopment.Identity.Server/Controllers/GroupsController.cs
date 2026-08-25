using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Application.Groups;
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
[Route("api/v1/groups")]
public sealed class GroupsController(
    IGroupService groupService,
    IAuditService auditService,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    [RequirePermission("identity-platform.group.read")]
    public async Task<IActionResult> GetGroups([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool allTenants = false, CancellationToken ct = default)
    {
        var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
        var result = await groupService.GetPagedAsync(search, page, pageSize, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
        return Ok(new
        {
            items = result.Items.Select(GroupResponse.From),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("identity-platform.group.read")]
    public async Task<IActionResult> GetGroupById(Guid id, [FromQuery] bool allTenants = false, CancellationToken ct = default)
    {
        var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
        var group = await groupService.GetAsync(id, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
        return group is null ? NotFound() : Ok(GroupResponse.From(group));
    }

    [HttpPost]
    [RequirePermission("identity-platform.group.create")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var group = await groupService.CreateAsync(request.Name, request.Description, request.ParentGroupId, tenantContext.TenantId, ct).ConfigureAwait(false);
            await auditService.RecordAsync("group.create", AuditOutcome.Success, tenantContext.TenantId, targetId: group.Id.ToString(), targetType: "group", ct: ct).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetGroupById), new { id = group.Id }, GroupResponse.From(group));
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid group", detail: ex.Message);
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

    [HttpPut("{id:guid}")]
    [RequirePermission("identity-platform.group.update")]
    public Task<IActionResult> UpdateGroup(Guid id, [FromBody] UpdateGroupRequest request, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "group.update", allTenants, canSeeAllTenants => groupService.UpdateAsync(id, request.Name, request.Description, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpPut("{id:guid}/parent")]
    [RequirePermission("identity-platform.group.update")]
    public Task<IActionResult> SetParent(Guid id, [FromBody] SetParentGroupRequest request, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "group.parent.set", allTenants, canSeeAllTenants => groupService.SetParentAsync(id, request.ParentGroupId, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission("identity-platform.group.delete")]
    public Task<IActionResult> DeleteGroup(Guid id, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "group.delete", allTenants, canSeeAllTenants => groupService.DeleteAsync(id, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpPut("{id:guid}/roles/{roleId:guid}")]
    [RequirePermission("identity-platform.group.update")]
    public Task<IActionResult> AssignRole(Guid id, Guid roleId, [FromBody] ScopedAssignmentRequest? request, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "group.role.assign", allTenants, canSeeAllTenants => groupService.AssignRoleAsync(id, roleId, request?.ScopeType, request?.ScopeValue, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [RequirePermission("identity-platform.group.update")]
    public Task<IActionResult> RemoveRole(Guid id, Guid roleId, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "group.role.remove", allTenants, canSeeAllTenants => groupService.RemoveRoleAsync(id, roleId, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    private async Task<bool> CanSeeAllTenantsAsync(bool requested, CancellationToken ct)
        => requested && await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false);

    private async Task<IActionResult> MutateAsync(Guid id, string action, bool allTenants, Func<bool, Task> mutation, CancellationToken ct)
    {
        try
        {
            var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
            await mutation(canSeeAllTenants).ConfigureAwait(false);
            await auditService.RecordAsync(action, AuditOutcome.Success, tenantContext.TenantId, targetId: id.ToString(), targetType: "group", ct: ct).ConfigureAwait(false);
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
