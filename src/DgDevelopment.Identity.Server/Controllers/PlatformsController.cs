using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Application.Platforms;
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
[Route("api/v1/platforms")]
public sealed class PlatformsController(
    IPlatformService platformService,
    IAuditService auditService,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    [RequirePermission("identity-platform.platform.read")]
    public async Task<IActionResult> GetPlatforms([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool allTenants = false, CancellationToken ct = default)
    {
        var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
        var result = await platformService.GetPagedAsync(search, page, pageSize, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
        return Ok(new
        {
            items = result.Items.Select(PlatformResponse.From),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("identity-platform.platform.read")]
    public async Task<IActionResult> GetPlatformById(Guid id, [FromQuery] bool allTenants = false, CancellationToken ct = default)
    {
        var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
        var platform = await platformService.GetAsync(id, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
        return platform is null ? NotFound() : Ok(PlatformResponse.From(platform));
    }

    [HttpPost]
    [RequirePermission("identity-platform.platform.create")]
    public async Task<IActionResult> CreatePlatform([FromBody] CreatePlatformRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var platform = await platformService.CreateAsync(request.Name, request.Description, request.PermissionMode, tenantContext.TenantId, ct).ConfigureAwait(false);
            await auditService.RecordAsync("platform.create", AuditOutcome.Success, tenantContext.TenantId, targetId: platform.Id.ToString(), targetType: "platform", ct: ct).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetPlatformById), new { id = platform.Id }, PlatformResponse.From(platform));
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid platform", detail: ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("identity-platform.platform.update")]
    public Task<IActionResult> UpdatePlatform(Guid id, [FromBody] UpdatePlatformRequest request, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "platform.update", allTenants, canSeeAllTenants => platformService.UpdateAsync(id, request.Name, request.Description, request.PermissionMode, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission("identity-platform.platform.delete")]
    public Task<IActionResult> DeletePlatform(Guid id, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "platform.delete", allTenants, canSeeAllTenants => platformService.DeleteAsync(id, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    private async Task<bool> CanSeeAllTenantsAsync(bool requested, CancellationToken ct)
        => requested && await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false);

    private async Task<IActionResult> MutateAsync(Guid id, string action, bool allTenants, Func<bool, Task> mutation, CancellationToken ct)
    {
        try
        {
            var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
            await mutation(canSeeAllTenants).ConfigureAwait(false);
            await auditService.RecordAsync(action, AuditOutcome.Success, tenantContext.TenantId, targetId: id.ToString(), targetType: "platform", ct: ct).ConfigureAwait(false);
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
