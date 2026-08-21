using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Server.Authorization;
using DgDevelopment.Identity.Server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DgDevelopment.Identity.Server.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/permissions")]
public sealed class PermissionsController(IPermissionRepository permissionRepository) : ControllerBase
{
    [HttpGet]
    [RequirePermission("identity-platform.permission.read")]
    public async Task<IActionResult> GetPermissions([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var permissions = (await permissionRepository.GetAllAsync(ct).ConfigureAwait(false))
            .Where(p => string.IsNullOrWhiteSpace(search) || p.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name)
            .ToList();

        var total = permissions.Count;
        var items = permissions.Skip((page - 1) * pageSize).Take(pageSize).Select(PermissionResponse.From);

        return Ok(new
        {
            items,
            page,
            pageSize,
            totalCount = total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }
}
