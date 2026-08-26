using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Server.Models;

public sealed record CreatePlatformRequest(string Name, string Description, PermissionMode PermissionMode);

public sealed record UpdatePlatformRequest(string Name, string Description, PermissionMode PermissionMode);

public sealed record PlatformResponse(Guid Id, Guid TenantId, string Name, string Description, PermissionMode PermissionMode)
{
    public static PlatformResponse From(Platform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        return new(platform.Id, platform.TenantId, platform.Name, platform.Description, platform.PermissionMode);
    }
}
