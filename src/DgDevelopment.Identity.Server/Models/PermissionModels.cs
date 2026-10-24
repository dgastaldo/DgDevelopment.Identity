using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Server.Models;

public sealed record PermissionResponse(Guid Id, Guid TenantId, Guid PlatformId, string Name, string Description, string ResourceType, bool IsGlobal)
{
    public static PermissionResponse From(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        return new(permission.Id, permission.TenantId, permission.PlatformId, permission.Name, permission.Description, permission.ResourceType, permission.IsGlobal);
    }
}
