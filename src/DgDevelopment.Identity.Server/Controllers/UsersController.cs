using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Application.Users;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Server.Authorization;
using DgDevelopment.Identity.Server.Data;
using DgDevelopment.Identity.Server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace DgDevelopment.Identity.Server.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/users")]
public sealed class UsersController(
    IUserService userService,
    IPermissionEvaluator permissionEvaluator,
    IAuditService auditService,
    ITenantContext tenantContext,
    IHostEnvironment hostEnvironment) : ControllerBase
{
    [HttpGet]
    [RequirePermission("identity-platform.user.read")]
    public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool allTenants = false, CancellationToken ct = default)
    {
        var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
        var result = await userService.GetPagedAsync(search, page, pageSize, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
        return Ok(new
        {
            items = result.Items.Select(UserResponse.From),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("identity-platform.user.read")]
    public async Task<IActionResult> GetUserById(Guid id, [FromQuery] bool allTenants = false, CancellationToken ct = default)
    {
        var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
        var user = await userService.GetAsync(id, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
        return user is null ? NotFound() : Ok(UserResponse.From(user));
    }

    [HttpPost]
    [RequirePermission("identity-platform.user.create")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var user = await userService.CreateAsync(request.Username, request.Password, request.Email, request.IsSystemAccount, tenantContext.TenantId, ct).ConfigureAwait(false);
            await auditService.RecordAsync("user.create", AuditOutcome.Success, tenantContext.TenantId, targetId: user.Id.ToString(), targetType: "user", ct: ct).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, UserResponse.From(user));
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid user", detail: ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("identity-platform.user.delete")]
    public Task<IActionResult> DeactivateUser(Guid id, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "user.deactivate", allTenants, canSeeAllTenants => userService.DeactivateAsync(id, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpPost("{id:guid}/lock")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> LockUser(Guid id, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "user.lock", allTenants, canSeeAllTenants => userService.SetLockAsync(id, true, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpPost("{id:guid}/unlock")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> UnlockUser(Guid id, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "user.unlock", allTenants, canSeeAllTenants => userService.SetLockAsync(id, false, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission("identity-platform.user.update")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, [FromQuery] bool allTenants, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
            var user = await userService.ResetPasswordAsync(id, request.Password, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
            await auditService.RecordAsync("user.reset-password", AuditOutcome.Success, tenantContext.TenantId, targetId: id.ToString(), targetType: "user", ct: ct).ConfigureAwait(false);

            // Keeps the local dev bootstrap snapshot (superadmin-credentials.txt) truthful whenever
            // the system account's own password is changed through the admin UI/API instead of the
            // one-time seed - only for IsSystemAccount, never for a regular tenant user's password.
            if (user.IsSystemAccount)
                SuperadminCredentialsWriter.Write(hostEnvironment.ContentRootPath, user.Username, user.PrimaryEmail?.Value ?? string.Empty, request.Password);

            return NoContent();
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentException)
        {
            return Problem(statusCode: ex is KeyNotFoundException ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    [HttpPut("{id:guid}/roles/{roleId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> AssignRole(Guid id, Guid roleId, [FromBody] ScopedAssignmentRequest? request, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "user.role.assign", allTenants, canSeeAllTenants => userService.AssignRoleAsync(id, roleId, request?.ScopeType, request?.ScopeValue, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> RemoveRole(Guid id, Guid roleId, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "user.role.remove", allTenants, canSeeAllTenants => userService.RemoveRoleAsync(id, roleId, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpPut("{id:guid}/permissions/{permissionId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> AssignPermission(Guid id, Guid permissionId, [FromBody] ScopedAssignmentRequest? request, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "user.permission.assign", allTenants, canSeeAllTenants => userService.AssignPermissionAsync(id, permissionId, request?.ScopeType, request?.ScopeValue, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpDelete("{id:guid}/permissions/{permissionId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> RemovePermission(Guid id, Guid permissionId, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "user.permission.remove", allTenants, canSeeAllTenants => userService.RemovePermissionAsync(id, permissionId, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpPut("{id:guid}/groups/{groupId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> AssignGroup(Guid id, Guid groupId, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "user.group.assign", allTenants, canSeeAllTenants => userService.AssignGroupAsync(id, groupId, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpDelete("{id:guid}/groups/{groupId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> RemoveGroup(Guid id, Guid groupId, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "user.group.remove", allTenants, canSeeAllTenants => userService.RemoveGroupAsync(id, groupId, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpGet("{id:guid}/permissions/effective")]
    [RequirePermission("identity-platform.user.read")]
    public async Task<IActionResult> GetEffectivePermissions(Guid id, [FromQuery] bool allTenants = false, CancellationToken ct = default)
    {
        var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
        if (await userService.GetAsync(id, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false) is null)
            return NotFound();

        var permissions = await permissionEvaluator.GetEffectivePermissionsAsync(id, tenantContext.TenantId, ct).ConfigureAwait(false);
        return Ok(permissions.Select(p => new UserPermissionResponse(p.Name, p.ScopeType, p.ScopeValue)));
    }

    private async Task<bool> CanSeeAllTenantsAsync(bool requested, CancellationToken ct)
        => requested && await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false);

    private async Task<IActionResult> MutateAsync(Guid id, string action, bool allTenants, Func<bool, Task> mutation, CancellationToken ct)
    {
        try
        {
            var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
            await mutation(canSeeAllTenants).ConfigureAwait(false);
            await auditService.RecordAsync(action, AuditOutcome.Success, tenantContext.TenantId, targetId: id.ToString(), targetType: "user", ct: ct).ConfigureAwait(false);
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
