using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Server.Models;

public sealed record CreateGroupRequest(string Name, string Description, Guid? ParentGroupId = null);

public sealed record UpdateGroupRequest(string Name, string Description);

public sealed record SetParentGroupRequest(Guid? ParentGroupId);

public sealed record GroupRoleResponse(Guid RoleId, string? ScopeType, string? ScopeValue);

public sealed record GroupResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Description,
    Guid? ParentGroupId,
    IReadOnlyCollection<GroupRoleResponse> Roles)
{
    public static GroupResponse From(Group group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return new(group.Id, group.TenantId, group.Name, group.Description, group.ParentGroupId,
            group.Roles.Select(r => new GroupRoleResponse(r.RoleId, r.ScopeType, r.ScopeValue)).ToList());
    }
}
