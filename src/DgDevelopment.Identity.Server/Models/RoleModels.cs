using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Server.Models;

public sealed record CreateRoleRequest(string Name, string Description, Guid PlatformId);

public sealed record UpdateRoleRequest(string Name, string Description);

public sealed record RolePermissionResponse(Guid PermissionId, string? ScopeType, string? ScopeValue);

public sealed record RoleResponse(
    Guid Id,
    Guid TenantId,
    Guid PlatformId,
    string Name,
    string Description,
    IReadOnlyCollection<RolePermissionResponse> Permissions)
{
    public static RoleResponse From(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return new(role.Id, role.TenantId, role.PlatformId, role.Name, role.Description,
            role.Permissions.Select(p => new RolePermissionResponse(p.PermissionId, p.ScopeType, p.ScopeValue)).ToList());
    }
}
