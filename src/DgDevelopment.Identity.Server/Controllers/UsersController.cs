using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Application.Users;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Server.Authorization;
using DgDevelopment.Identity.Server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DgDevelopment.Identity.Server.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/users")]
public sealed class UsersController(
    IUserService userService,
    IPermissionEvaluator permissionEvaluator,
    IAuditService auditService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("identity-platform.user.read")]
    public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await userService.GetPagedAsync(search, page, pageSize, ct).ConfigureAwait(false);
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
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct)
    {
        var user = await userService.GetAsync(id, ct).ConfigureAwait(false);
        return user is null ? NotFound() : Ok(UserResponse.From(user));
    }

    [HttpPost]
    [RequirePermission("identity-platform.user.create")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var user = await userService.CreateAsync(request.Username, request.Password, request.Email, request.IsSystemAccount, ct).ConfigureAwait(false);
            await auditService.RecordAsync("user.create", AuditOutcome.Success, targetId: user.Id.ToString(), targetType: "user", ct: ct).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, UserResponse.From(user));
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid user", detail: ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("identity-platform.user.delete")]
    public Task<IActionResult> DeactivateUser(Guid id, CancellationToken ct)
        => MutateAsync(id, "user.deactivate", () => userService.DeactivateAsync(id, ct), ct);

    [HttpPost("{id:guid}/lock")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> LockUser(Guid id, CancellationToken ct)
        => MutateAsync(id, "user.lock", () => userService.SetLockAsync(id, true, ct), ct);

    [HttpPost("{id:guid}/unlock")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> UnlockUser(Guid id, CancellationToken ct)
        => MutateAsync(id, "user.unlock", () => userService.SetLockAsync(id, false, ct), ct);

    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission("identity-platform.user.update")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            await userService.ResetPasswordAsync(id, request.Password, ct).ConfigureAwait(false);
            await auditService.RecordAsync("user.reset-password", AuditOutcome.Success, targetId: id.ToString(), targetType: "user", ct: ct).ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentException)
        {
            return Problem(statusCode: ex is KeyNotFoundException ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    [HttpPut("{id:guid}/roles/{roleId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> AssignRole(Guid id, Guid roleId, [FromBody] ScopedAssignmentRequest? request, CancellationToken ct)
        => MutateAsync(id, "user.role.assign", () => userService.AssignRoleAsync(id, roleId, request?.ScopeType, request?.ScopeValue, ct), ct);

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> RemoveRole(Guid id, Guid roleId, CancellationToken ct)
        => MutateAsync(id, "user.role.remove", () => userService.RemoveRoleAsync(id, roleId, ct), ct);

    [HttpPut("{id:guid}/permissions/{permissionId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> AssignPermission(Guid id, Guid permissionId, [FromBody] ScopedAssignmentRequest? request, CancellationToken ct)
        => MutateAsync(id, "user.permission.assign", () => userService.AssignPermissionAsync(id, permissionId, request?.ScopeType, request?.ScopeValue, ct), ct);

    [HttpDelete("{id:guid}/permissions/{permissionId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> RemovePermission(Guid id, Guid permissionId, CancellationToken ct)
        => MutateAsync(id, "user.permission.remove", () => userService.RemovePermissionAsync(id, permissionId, ct), ct);

    [HttpPut("{id:guid}/groups/{groupId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> AssignGroup(Guid id, Guid groupId, CancellationToken ct)
        => MutateAsync(id, "user.group.assign", () => userService.AssignGroupAsync(id, groupId, ct), ct);

    [HttpDelete("{id:guid}/groups/{groupId:guid}")]
    [RequirePermission("identity-platform.user.update")]
    public Task<IActionResult> RemoveGroup(Guid id, Guid groupId, CancellationToken ct)
        => MutateAsync(id, "user.group.remove", () => userService.RemoveGroupAsync(id, groupId, ct), ct);

    [HttpGet("{id:guid}/permissions/effective")]
    [RequirePermission("identity-platform.user.read")]
    public async Task<IActionResult> GetEffectivePermissions(Guid id, CancellationToken ct)
    {
        if (await userService.GetAsync(id, ct).ConfigureAwait(false) is null)
            return NotFound();

        var permissions = await permissionEvaluator.GetEffectivePermissionsAsync(id, ct).ConfigureAwait(false);
        return Ok(permissions.Select(p => new UserPermissionResponse(p.Name, p.ScopeType, p.ScopeValue)));
    }

    private async Task<IActionResult> MutateAsync(Guid id, string action, Func<Task> mutation, CancellationToken ct)
    {
        try
        {
            await mutation().ConfigureAwait(false);
            await auditService.RecordAsync(action, AuditOutcome.Success, targetId: id.ToString(), targetType: "user", ct: ct).ConfigureAwait(false);
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
